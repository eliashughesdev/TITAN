from datetime import datetime
from typing import Literal
import logging

from fastapi import APIRouter, Depends, HTTPException
from pydantic import Field

from app.core.deps import require_permission
from app.core.safety import MutationFlags, assert_live, preview
from app.services.collaborators import get_collab
from app.services.database import fetch_all, get_connection
from app.services.zk_devices import load_devices

router = APIRouter()
log = logging.getLogger(__name__)
VERSION = "daily-row-v2"


class RemotePunchIn(MutationFlags):
    codigo: str = Field(min_length=1, max_length=24)
    tipo: Literal["entrada", "salida"] = "entrada"
    dispositivo: str = Field(
        default="Ponche Remoto",
        max_length=200,
    )
    target: Literal["sql", "sql_and_clock"] = "sql"


def employee(codigo: str) -> dict:
    profile = get_collab(codigo)

    if profile and not profile.get("activo", True):
        raise HTTPException(
            409,
            "El colaborador está inactivo",
        )

    if profile and str(profile.get("nombre") or "").strip():
        return profile

    rows = fetch_all(
        """
        SELECT TOP 1 codigo, nombre, departamento
        FROM dbo.punches
        WHERE codigo = ?
        ORDER BY fecha DESC, id DESC
        """,
        (codigo,),
    )

    if profile:
        saved = rows[0] if rows else {}
        return {
            **saved,
            **profile,
            "nombre": (
                profile.get("nombre")
                or saved.get("nombre")
            ),
            "departamento": (
                profile.get("departamento")
                or saved.get("departamento")
            ),
        }

    if not rows:
        raise HTTPException(
            404,
            "Colaborador no encontrado; crea su ficha primero",
        )

    return rows[0]


def daily_action(row, tipo: str) -> str:
    if row is None:
        return "insert"

    field_index = 1 if tipo == "entrada" else 2

    if row[field_index] is not None:
        raise HTTPException(
            409,
            f"Ya existe una {tipo} para este colaborador hoy. "
            "Revisa el historial; no se sobrescribió el registro.",
        )

    return "update"


@router.post("/remote-punch")
def remote_punch(
    body: RemotePunchIn,
    user: dict = Depends(
        require_permission("remote_punch")
    ),
):
    assert_live(
        body.dry_run,
        body.confirm,
        "ponche remoto",
    )

    codigo = body.codigo.strip()

    if not codigo.isascii() or not codigo.isdigit():
        raise HTTPException(
            400,
            "El código debe contener solo números",
        )

    if body.target == "sql_and_clock":
        raise HTTPException(
            501,
            "La escritura física está pendiente de "
            "compatibilidad del SDK. No se guardó este ponche.",
        )

    person = employee(codigo)
    device = body.dispositivo.strip() or "Ponche Remoto"

    if device != "Ponche Remoto":
        names = {
            str(item.get("name") or "").strip()
            for item in load_devices()
        }

        if device not in names:
            raise HTTPException(
                400,
                "Selecciona un reloj registrado o Ponche Remoto",
            )

    origin = (
        "Ponche Remoto"
        if device == "Ponche Remoto"
        else f"{device} (remoto)"
    )

    if len(origin) > 200:
        raise HTTPException(
            400,
            "Nombre de origen demasiado largo",
        )

    actor = str(
        user.get("username")
        or user.get("id")
        or "authenticated"
    )

    stamp = datetime.now()
    today = stamp.strftime("%Y-%m-%d")
    hour = stamp.strftime("%H:%M:%S")

    if body.dry_run:
        rows = fetch_all(
            """
            SELECT id, entrada, salida
            FROM dbo.punches
            WHERE codigo = ? AND fecha = ?
            """,
            (codigo, today),
        )

        row = (
            (
                rows[0]["id"],
                rows[0]["entrada"],
                rows[0]["salida"],
            )
            if rows else None
        )

        action = daily_action(row, body.tipo)

        return preview(
            "registrar ponche en SQL",
            codigo=codigo,
            nombre=person.get("nombre"),
            tipo=body.tipo,
            dispositivo=device,
            origen=origin,
            actor=actor,
            action=action,
            version=VERSION,
            sql_confirmed=False,
            clock_confirmed=False,
            message=(
                "Se completará la fila diaria en SQL; "
                "no se escribirá en la memoria del reloj."
            ),
        )

    connection = None
    cursor = None

    try:
        connection = get_connection()
        connection.autocommit = False
        cursor = connection.cursor()

        cursor.execute(
            "SET XACT_ABORT ON; BEGIN TRANSACTION"
        )

        cursor.execute(
            """
            DECLARE @r int;

            EXEC @r = sys.sp_getapplock
                @Resource = ?,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 8000;

            SELECT @r;
            """,
            (f"titan:remote-punch:{codigo}:{today}",),
        )

        lock = cursor.fetchone()

        if not lock or int(lock[0]) < 0:
            raise HTTPException(
                409,
                "Hay otra operación en curso; vuelve a intentar",
            )

        cursor.execute(
            """
            SELECT id, entrada, salida
            FROM dbo.punches WITH (UPDLOCK, HOLDLOCK)
            WHERE codigo = ? AND fecha = ?
            """,
            (codigo, today),
        )

        row = cursor.fetchone()
        action = daily_action(row, body.tipo)

        if action == "update":
            record_id = row[0]

            # Completa únicamente el campo vacío.
            # Conserva el resto de la fila diaria.
            sql = (
                "UPDATE dbo.punches SET entrada=? WHERE id=?"
                if body.tipo == "entrada"
                else
                "UPDATE dbo.punches SET salida=? WHERE id=?"
            )

            cursor.execute(
                sql,
                (hour, record_id),
            )

        else:
            cursor.execute(
                """
                INSERT INTO dbo.punches (
                    codigo,
                    nombre,
                    departamento,
                    fecha,
                    entrada,
                    salida,
                    dispositivo_origen
                )
                OUTPUT INSERTED.id
                VALUES (?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    codigo,
                    person.get("nombre"),
                    person.get("departamento"),
                    today,
                    hour if body.tipo == "entrada" else None,
                    hour if body.tipo == "salida" else None,
                    origin,
                ),
            )

            record_id = cursor.fetchone()[0]

        connection.commit()

    except HTTPException:
        if connection is not None:
            connection.rollback()
        raise

    except Exception:
        if connection is not None:
            connection.rollback()

        log.exception(
            "Falló el registro remoto diario en SQL"
        )

        raise HTTPException(
            503,
            "No se pudo confirmar el registro en SQL; "
            "revisa el backend y el historial antes de repetirlo",
        )

    finally:
        if cursor is not None:
            cursor.close()

        if connection is not None:
            connection.close()

    return {
        "status": "sql_confirmed",
        "version": VERSION,
        "id": record_id,
        "codigo": codigo,
        "nombre": person.get("nombre"),
        "tipo": body.tipo,
        "fecha": today,
        "hora": hour,
        "dispositivo": device,
        "actor": actor,
        "sql_confirmed": True,
        "clock_confirmed": False,
        "action": action,
        "message": (
            "Registro confirmado en SQL. "
            "No fue insertado en la memoria del reloj."
        ),
    }


@router.get("/remote-devices")
def remote_devices(
    user: dict = Depends(
        require_permission("remote_punch")
    ),
):
    names = sorted({
        str(item.get("name") or "").strip()
        for item in load_devices()
        if item.get("name")
    })

    return {
        "version": VERSION,
        "items": ["Ponche Remoto"] + [
            name
            for name in names
            if name != "Ponche Remoto"
        ],
    }