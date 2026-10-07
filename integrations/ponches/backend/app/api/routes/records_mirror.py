from __future__ import annotations

import asyncio
import logging
import time
from contextlib import asynccontextmanager, suppress

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field

from app.core.deps import require_permission
from app.services import clock_mirror, profile_mirror
from app.services.zk_devices import find_device, load_devices

log = logging.getLogger(__name__)


def require_titan(user: dict) -> None:
    if (
        user.get("source") != "titan"
        or not user.get("organization_id")
    ):
        raise HTTPException(
            status_code=403,
            detail="Esta operación requiere sesión TitanMDM",
        )


async def run_worker():
    while True:
        try:
            await asyncio.to_thread(
                profile_mirror.flush
            )

            found = await asyncio.to_thread(
                clock_mirror.process_one
            )

        except Exception:
            log.exception(
                "No se pudo procesar la cola "
                "de escritura de relojes"
            )
            found = False

        await asyncio.sleep(
            1 if found else 5
        )


@asynccontextmanager
async def mirror_lifespan(app):
    worker = asyncio.create_task(
        run_worker()
    )

    try:
        yield
    finally:
        worker.cancel()

        with suppress(
            asyncio.CancelledError
        ):
            await worker


router = APIRouter(
    prefix="/collab-reconcile/mirror",
    tags=["Clock mirror"],
    lifespan=mirror_lifespan,
)


class RenameRequest(BaseModel):
    codigo: str = Field(
        min_length=1,
        max_length=24,
    )

    nombre: str = Field(
        min_length=1,
        max_length=80,
    )

    devices: list[str] = Field(
        min_length=1,
        max_length=20,
    )

    confirm: bool = False


class RetryRequest(BaseModel):
    confirm: bool = False


@router.get("/devices")
def devices(
    user: dict = Depends(
        require_permission(
            "collaborators.sync"
        )
    ),
):
    require_titan(user)

    return {
        "items": [
            {
                "name": item.get("name"),
            }
            for item in load_devices()
            if item.get("name")
        ],
    }


@router.get("/jobs")
def jobs(
    user: dict = Depends(
        require_permission(
            "collaborators.sync"
        )
    ),
):
    require_titan(user)

    items = clock_mirror.history(
        user["organization_id"]
    )

    items += profile_mirror.pending_history(
        user["organization_id"]
    )

    items.sort(
        key=lambda item: item["created_at"],
        reverse=True,
    )

    # La respuesta pública excluye payload y result.
    # El payload interno puede contener el PIN del reloj.
    public_fields = (
        "id",
        "device",
        "codigo",
        "nombre",
        "status",
        "attempts",
        "created_at",
        "updated_at",
        "detail",
        "fields",
    )

    return {
        "items": [
            {
                key: item.get(key)
                for key in public_fields
            }
            for item in items[:100]
        ],
    }


@router.post(
    "/rename",
    status_code=202,
)
def rename(
    body: RenameRequest,
    user: dict = Depends(
        require_permission("zk.push")
    ),
):
    require_titan(user)

    if not body.confirm:
        raise HTTPException(
            status_code=400,
            detail=(
                "Confirma la escritura "
                "en los relojes"
            ),
        )

    codigo = body.codigo.strip()

    try:
        nombre = clock_mirror.validate_name(
            body.nombre
        )
    except ValueError as error:
        raise HTTPException(
            status_code=400,
            detail=str(error),
        )

    if (
        not codigo.isascii()
        or not codigo.isdigit()
    ):
        raise HTTPException(
            status_code=400,
            detail=(
                "El código debe contener solo números; "
                "conserva los ceros iniciales"
            ),
        )

    names = list(
        dict.fromkeys(
            name.strip()
            for name in body.devices
            if name.strip()
        )
    )

    if not names:
        raise HTTPException(
            status_code=400,
            detail="Selecciona al menos un reloj",
        )

    for name in names:
        if not find_device(name):
            raise HTTPException(
                status_code=400,
                detail=(
                    f"Reloj no registrado: {name}"
                ),
            )

    items = [
        clock_mirror.enqueue(
            user["organization_id"],
            user["username"],
            name,
            codigo,
            nombre,
        )
        for name in names
    ]

    return {
        "accepted": True,
        "confirmed": False,
        "items": items,
        "message": (
            "Operaciones en cola; revisa "
            "la confirmación de cada reloj"
        ),
    }


@router.post(
    "/jobs/{job_id}/retry",
    status_code=202,
)
def retry(
    job_id: str,
    body: RetryRequest,
    user: dict = Depends(
        require_permission(
            "collaborators.sync"
        )
    ),
):
    require_titan(user)

    if not body.confirm:
        raise HTTPException(
            status_code=400,
            detail=(
                "Confirma el reintento "
                "de escritura"
            ),
        )

    with clock_mirror.database() as connection:
        connection.execute(
            "BEGIN IMMEDIATE"
        )

        job = connection.execute(
            """
            SELECT *
            FROM mirror_jobs
            WHERE id = ?
              AND organization = ?
            """,
            (
                job_id,
                user["organization_id"],
            ),
        ).fetchone()

        if not job:
            raise HTTPException(
                status_code=404,
                detail="Operación no encontrada",
            )

        if job["status"] not in (
            "retry",
            "failed",
            "blocked",
        ):
            raise HTTPException(
                status_code=409,
                detail=(
                    "Esta operación no admite "
                    "reintento"
                ),
            )

        newer = connection.execute(
            """
            SELECT id
            FROM mirror_jobs
            WHERE organization = ?
              AND device = ?
              AND codigo = ?
              AND created_at > ?
              AND status != 'superseded'
            LIMIT 1
            """,
            (
                job["organization"],
                job["device"],
                job["codigo"],
                job["created_at"],
            ),
        ).fetchone()

        if newer:
            raise HTTPException(
                status_code=409,
                detail=(
                    "Existe un cambio posterior; "
                    "utiliza la ficha actual "
                    "del colaborador"
                ),
            )

        connection.execute(
            """
            UPDATE mirror_jobs
            SET
                status = 'pending',
                attempts = 0,
                next_at = ?,
                lease_until = 0,
                updated_at = ?,
                actor = ?,
                detail = ?
            WHERE id = ?
            """,
            (
                time.time(),
                clock_mirror.now(),
                user["username"],
                (
                    "Reintento solicitado "
                    "desde TitanMDM"
                ),
                job_id,
            ),
        )

        connection.commit()

    return {
        "accepted": True,
        "confirmed": False,
        "id": job_id,
        "message": (
            "Reintento en cola; pendiente "
            "de confirmación física"
        ),
    }