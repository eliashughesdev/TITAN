from datetime import date, datetime
import logging

from fastapi import APIRouter, Depends, HTTPException, Query
from pydantic import Field

from app.core.deps import require_permission
from app.core.safety import MutationFlags, assert_live
from app.services.database import fetch_all, get_connection
from app.services.sync_log import append_sync, load_log
from app.services.zk_devices import (
    _connect,
    find_device,
    load_devices,
)

router = APIRouter()
log = logging.getLogger(__name__)


class SyncRequest(MutationFlags):
    device: str = Field(min_length=1, max_length=200)
    start: date
    end: date


def group_attendance(events, users, start, end):
    names = {
        str(user.user_id): str(user.name or "").strip()
        for user in users
    }

    grouped = {}
    ignored = 0

    for event in events:
        stamp = getattr(event, "timestamp", None)

        if not isinstance(stamp, datetime):
            continue

        if not start <= stamp.date() <= end:
            continue

        code = str(
            getattr(event, "user_id", "") or ""
        ).strip()

        # Estados explícitos del reloj: 0=entrada y 1=salida.
        kind = getattr(event, "punch", None)

        if (
            not code.isascii()
            or not code.isdigit()
            or kind not in (0, 1)
        ):
            ignored += 1
            continue

        key = (code, stamp.date().isoformat())

        row = grouped.setdefault(
            key,
            {
                "codigo": code,
                "fecha": key[1],
                "nombre": names.get(code, ""),
                "entrada": None,
                "salida": None,
            },
        )

        hour = stamp.strftime("%H:%M:%S")
        field = "entrada" if kind == 0 else "salida"
        old = row[field]

        if (
            old is None
            or (kind == 0 and hour < old)
            or (kind == 1 and hour > old)
        ):
            row[field] = hour

    return [
        grouped[key]
        for key in sorted(grouped)
    ], ignored


def merge_daily(existing, incoming):
    if existing is None:
        return incoming["entrada"], incoming["salida"], True

    entry = (
        existing[1]
        if existing[1] is not None
        else incoming["entrada"]
    )

    exit_ = (
        existing[2]
        if existing[2] is not None
        else incoming["salida"]
    )

    changed = (
        existing[1] is None
        and incoming["entrada"] is not None
    ) or (
        existing[2] is None
        and incoming["salida"] is not None
    )

    return entry, exit_, changed


@router.get("/sync-history")
def sync_history(
    limit: int = Query(150, ge=1, le=500),
    user: dict = Depends(require_permission("sync.read")),
):
    items = list(reversed(load_log()))[:limit]

    names = sorted({
        str(device.get("name") or "").strip()
        for device in load_devices()
        if str(device.get("name") or "").strip()
    })

    # Se devuelve únicamente el nombre, sin claves de comunicación.
    return {
        "source": "app_sync_log",
        "count": len(items),
        "items": items,
        "columns": [
            "fecha",
            "evento",
            "detalle",
            "registros",
            "estado",
        ],
        "devices": [
            {"name": name}
            for name in names
        ],
    }


@router.post("/sync-now")
def sync_now(
    body: SyncRequest,
    user: dict = Depends(require_permission("sync.run")),
):
    assert_live(
        body.dry_run,
        body.confirm,
        "importar asistencia",
    )

    if body.end < body.start or (body.end - body.start).days > 6:
        raise HTTPException(
            400,
            "Selecciona un intervalo de uno a siete días",
        )

    if body.end > date.today():
        raise HTTPException(
            400,
            "No se pueden importar fechas futuras",
        )

    device = find_device(body.device.strip())

    if not device or not device.get("ip"):
        raise HTTPException(
            400,
            "Reloj no registrado o sin IP",
        )

    clock = None

    try:
        clock = _connect(device, timeout=12)
        users = clock.get_users() or []
        events = clock.get_attendance() or []

        candidates, ignored = group_attendance(
            events,
            users,
            body.start,
            body.end,
        )

    except Exception:
        log.exception("Falló la lectura de asistencia del reloj")
        raise HTTPException(
            503,
            "No se pudo leer la asistencia del reloj; "
            "no se escribió en SQL",
        )

    finally:
        if clock is not None:
            try:
                clock.disconnect()
            except Exception:
                log.warning(
                    "No se confirmó el cierre de conexión del reloj"
                )

    if body.dry_run:
        existing_rows = fetch_all(
            """
            SELECT id, codigo, fecha, entrada, salida
            FROM dbo.punches
            WHERE fecha >= ? AND fecha <= ?
            """,
            (
                body.start.isoformat(),
                body.end.isoformat(),
            ),
        )

        existing = {
            (str(row["codigo"]), str(row["fecha"])[:10]): row
            for row in existing_rows
        }

        inserts = updates = unchanged = 0

        for row in candidates:
            saved = existing.get(
                (row["codigo"], row["fecha"])
            )

            values = (
                (saved["id"], saved["entrada"], saved["salida"])
                if saved
                else None
            )

            _, _, changed = merge_daily(values, row)

            if saved is None:
                inserts += 1
            elif changed:
                updates += 1
            else:
                unchanged += 1

        return {
            "mode": "dry_run",
            "device": body.device,
            "candidates": len(candidates),
            "inserts": inserts,
            "updates": updates,
            "unchanged": unchanged,
            "ignored": ignored,
            "items": candidates[:100],
            "sql_confirmed": False,
            "message": (
                "Vista previa. Se conservan los campos existentes; "
                "se completan únicamente los vacíos."
            ),
        }

    connection = None
    cursor = None
    inserted = updated = unchanged = 0

    try:
        connection = get_connection()
        connection.autocommit = False
        cursor = connection.cursor()
        cursor.execute("SET XACT_ABORT ON; BEGIN TRANSACTION")

        for row in candidates:
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
                (
                    f"titan:remote-punch:"
                    f"{row['codigo']}:{row['fecha']}",
                ),
            )

            lock = cursor.fetchone()

            if not lock or int(lock[0]) < 0:
                raise HTTPException(
                    409,
                    "Hay otra operación en curso; "
                    "la importación fue revertida",
                )

            cursor.execute(
                """
                SELECT id, entrada, salida
                FROM dbo.punches WITH (UPDLOCK, HOLDLOCK)
                WHERE codigo = ? AND fecha = ?
                """,
                (row["codigo"], row["fecha"]),
            )

            saved = cursor.fetchone()
            entry, exit_, changed = merge_daily(saved, row)

            if saved is None:
                cursor.execute(
                    """
                    INSERT INTO dbo.punches (
                        codigo, nombre, departamento, fecha,
                        entrada, salida, dispositivo_origen
                    )
                    VALUES (?, ?, ?, ?, ?, ?, ?)
                    """,
                    (
                        row["codigo"],
                        row["nombre"] or None,
                        None,
                        row["fecha"],
                        entry,
                        exit_,
                        device["name"],
                    ),
                )
                inserted += 1

            elif changed:
                cursor.execute(
                    """
                    UPDATE dbo.punches
                    SET entrada = ?, salida = ?
                    WHERE id = ?
                    """,
                    (entry, exit_, saved[0]),
                )
                updated += 1

            else:
                unchanged += 1

        connection.commit()

    except HTTPException:
        if connection is not None:
            connection.rollback()
        raise

    except Exception:
        if connection is not None:
            connection.rollback()

        log.exception("Falló la importación de asistencia en SQL")
        raise HTTPException(
            503,
            "No se pudo confirmar la importación; "
            "revisa el backend y el historial",
        )

    finally:
        if cursor is not None:
            cursor.close()
        if connection is not None:
            connection.close()

    detail = (
        f"{device['name']}: nuevas={inserted}, "
        f"completadas={updated}, sin cambios={unchanged}, "
        f"ignoradas={ignored}"
    )

    warning = ""

    try:
        append_sync(
            "clock_import",
            detail,
            inserted + updated,
            "ok",
        )
    except Exception:
        log.exception(
            "SQL confirmado pero falló la bitácora de sincronización"
        )
        warning = (
            "SQL confirmado; no se pudo guardar la bitácora local."
        )

    return {
        "mode": "live",
        "sql_confirmed": True,
        "device": body.device,
        "inserted": inserted,
        "updated": updated,
        "unchanged": unchanged,
        "ignored": ignored,
        "message": detail,
        "warning": warning,
    }