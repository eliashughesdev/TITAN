from __future__ import annotations

from typing import Any


MODULE_ROUTES: dict[str, str] = {
    # Dashboard
    "dashboard": "/dashboard",
    "inicio": "/dashboard",
    "panel": "/dashboard",

    # Ponches
    "ponches": "/records",
    "records": "/records",
    "registros": "/records",

    # Historial SQL
    "historial sql": "/db-records",
    "historial_sql": "/db-records",
    "db-records": "/db-records",
    "db_records": "/db-records",

    # Ponche remoto
    "ponche remoto": "/remote-punch",
    "ponche_remoto": "/remote-punch",
    "remote-punch": "/remote-punch",
    "remote_punch": "/remote-punch",

    # Dispositivos
    "dispositivos": "/devices",
    "relojes": "/devices",
    "devices": "/devices",

    # Empleados
    "empleados": "/employees",
    "employees": "/employees",

    # Colaboradores
    "colaboradores": "/collaborators",
    "collaborators": "/collaborators",

    # Horarios
    "horarios": "/schedules",
    "schedules": "/schedules",
    "turnos": "/schedules",

    # Inventario biométrico
    "inventario biometrico": "/biometric",
    "inventario biométrico": "/biometric",
    "biometrico": "/biometric",
    "biométrico": "/biometric",
    "biometric": "/biometric",

    # Operaciones masivas
    "operaciones masivas": "/bulk",
    "operaciones_masivas": "/bulk",
    "bulk": "/bulk",

    # Reportes
    "reportes": "/reports",
    "reports": "/reports",

    # Exportaciones
    "exportar": "/export",
    "exportar datos": "/export",
    "export": "/export",

    # Historial sync
    "historial sync": "/sync-history",
    "historial_sync": "/sync-history",
    "sync-history": "/sync-history",
    "sync_history": "/sync-history",

    # Usuarios
    "usuarios": "/users",
    "users": "/users",

    # Configuración
    "configuracion": "/settings",
    "configuración": "/settings",
    "settings": "/settings",

    # Reportes avanzados
    "reportes avanzados": "/advanced-reports",
    "reportes_avanzados": "/advanced-reports",
    "advanced-reports": "/advanced-reports",
    "advanced_reports": "/advanced-reports",
}


def normalize_module_name(module: str) -> str:
    return (
        str(module or "")
        .strip()
        .lower()
    )


def navigate_to_module(
    user: dict[str, Any],
    module: str,
) -> dict[str, Any]:

    key = normalize_module_name(module)

    route = MODULE_ROUTES.get(key)

    if not route:
        return {
            "ok": False,
            "error": "Módulo no reconocido.",
            "allowed": sorted(
                set(MODULE_ROUTES.keys())
            ),
        }

    return {
        "ok": True,
        "action": {
            "type": "navigate",
            "route": route,
        },
        "module": key,
        "route": route,
    }