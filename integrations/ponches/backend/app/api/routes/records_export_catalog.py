from fastapi import APIRouter, Depends, HTTPException

from app.core.deps import require_permission
from app.services.database import fetch_all, test_connection
from app.api.routes.records_shared import DEVICES_FILE, read_json

router = APIRouter()


@router.get("/export-devices")
def export_devices(
    _user: dict = Depends(require_permission("exports.read")),
):
    saved = read_json(DEVICES_FILE, [])

    if not isinstance(saved, list):
        raise HTTPException(
            status_code=500,
            detail="El inventario de relojes tiene un formato incorrecto.",
        )

    catalog = {}

    for device in saved:
        if not isinstance(device, dict):
            continue

        name = str(device.get("name") or "").strip()

        if not name:
            continue

        catalog[name.casefold()] = {
            "dispositivo": name,
            "total_registros": 0,
            "registrado": True,
        }

    database = test_connection()
    online = database.get("status") == "online"

    if online:
        try:
            rows = fetch_all(
                """
                SELECT
                    LTRIM(RTRIM(dispositivo_origen)) AS dispositivo,
                    COUNT_BIG(*) AS total_registros
                FROM [dbo].[punches]
                WHERE dispositivo_origen IS NOT NULL
                  AND LTRIM(RTRIM(dispositivo_origen)) <> ''
                GROUP BY LTRIM(RTRIM(dispositivo_origen))
                """
            )
        except Exception:
            raise HTTPException(
                status_code=503,
                detail="No se pudo consultar el catálogo de ponches en SQL.",
            )

        for row in rows:
            name = str(row.get("dispositivo") or "").strip()

            if not name:
                continue

            key = name.casefold()

            if key not in catalog:
                catalog[key] = {
                    "dispositivo": name,
                    "total_registros": 0,
                    "registrado": False,
                }

            catalog[key]["total_registros"] += int(
                row.get("total_registros") or 0
            )

    items = sorted(
        catalog.values(),
        key=lambda item: item["dispositivo"].casefold(),
    )

    return {
        "items": items,
        "count": len(items),
        "sql_online": online,
        "warning": (
            None
            if online
            else (
                "Los relojes registrados están disponibles, "
                "pero SQL está desconectado. "
                "La exportación requiere conexión con SQL."
            )
        ),
    }