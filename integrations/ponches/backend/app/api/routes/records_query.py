"""Consultas de ponches y dashboard; no modifica registros históricos."""

from datetime import datetime

from fastapi import APIRouter, Depends, HTTPException, Query

from app.core.deps import require_permission
from app.services.audit import recent as load_audit
from app.services.collaborators import load_collabs
from app.services.database import fetch_all, test_connection
from app.services.sync_log import append_sync

router = APIRouter()

READ = Depends(require_permission("attendance.read"))

COLUMNS = (
    "id, codigo, nombre, departamento, fecha, entrada, salida, "
    "dispositivo_origen, ultima_sincronizacion"
)

TODAY = (
    "fecha >= CAST(GETDATE() AS date) "
    "AND fecha < DATEADD(day, 1, CAST(GETDATE() AS date))"
)

YESTERDAY = (
    "fecha >= DATEADD(day, -1, CAST(GETDATE() AS date)) "
    "AND fecha < CAST(GETDATE() AS date)"
)


def _require_db():
    db = test_connection()

    if db["status"] != "online":
        raise HTTPException(503, db["detail"])

    return db


def current_departments():
    """Personas con registros hoy según el departamento actual de su ficha."""

    profiles = {
        str(p.get("codigo") or "").strip():
        str(p.get("departamento") or "").strip()
        for p in load_collabs()
    }

    rows = fetch_all(f"""
        SELECT
            codigo,
            MAX(NULLIF(LTRIM(RTRIM(departamento)), '')) AS departamento
        FROM dbo.punches
        WHERE {TODAY}
        GROUP BY codigo
    """)

    totals = {}

    for row in rows:
        code = str(row.get("codigo") or "").strip()

        department = (
            profiles.get(code)
            or row.get("departamento")
            or "Sin departamento"
        )

        totals[department] = totals.get(department, 0) + 1

    return [
        {"depto": name, "total": total}
        for name, total in sorted(
            totals.items(),
            key=lambda p: (-p[1], p[0]),
        )
    ]


def day_summary(where):
    rows = fetch_all(f"""
        SELECT
            COUNT(*) AS total_hoy,
            SUM(
                CASE WHEN entrada IS NOT NULL THEN 1 ELSE 0 END
            ) AS con_entrada,
            SUM(
                CASE WHEN salida IS NOT NULL THEN 1 ELSE 0 END
            ) AS con_salida,
            COUNT(DISTINCT codigo) AS empleados_hoy,
            COUNT(DISTINCT dispositivo_origen) AS dispositivos_hoy,
            SUM(
                CASE
                    WHEN entrada IS NOT NULL AND salida IS NULL
                    THEN 1 ELSE 0
                END
            ) AS sin_salida
        FROM dbo.punches
        WHERE {where}
    """)

    if not rows:
        return {}

    return {
        key: int(value or 0)
        for key, value in rows[0].items()
    }


def open_shifts():
    return fetch_all(f"""
        SELECT TOP 20
            codigo,
            nombre,
            dispositivo_origen,
            CONVERT(varchar(8), entrada, 108) AS entrada
        FROM dbo.punches
        WHERE {TODAY}
          AND entrada IS NOT NULL
          AND salida IS NULL
        ORDER BY entrada DESC
    """)


def trends():
    days = fetch_all("""
        SELECT
            CONVERT(varchar(10), CAST(fecha AS date), 23) AS dia,
            COUNT(*) AS total
        FROM dbo.punches
        WHERE fecha >= DATEADD(day, -13, CAST(GETDATE() AS date))
          AND fecha < DATEADD(day, 1, CAST(GETDATE() AS date))
        GROUP BY CAST(fecha AS date)
        ORDER BY CAST(fecha AS date)
    """)

    hours = fetch_all(f"""
        SELECT
            DATEPART(hour, entrada) AS hora,
            COUNT(*) AS total
        FROM dbo.punches
        WHERE {TODAY}
          AND entrada IS NOT NULL
        GROUP BY DATEPART(hour, entrada)
        ORDER BY hora
    """)

    return {
        "by_day": days,
        "by_hour": hours,
        "by_device": [],
        "activity": [],
    }


def overview(today, yesterday):
    return {
        "today": {
            "total": today.get("total_hoy", 0),
            "empleados": today.get("empleados_hoy", 0),
            "relojes": today.get("dispositivos_hoy", 0),
            "sin_salida": today.get("sin_salida", 0),
        },
        "yesterday": {
            "total": yesterday.get("total_hoy", 0),
            "empleados": yesterday.get("empleados_hoy", 0),
        },
        "open_shifts": open_shifts(),
        "by_dept": current_departments(),
        "sql_online": True,
    }


@router.get("/recent")
def punches_recent(
    limit: int = Query(50, ge=1, le=200),
    _user: dict = READ,
):
    _require_db()

    rows = fetch_all(
        f"""
        SELECT TOP (?) {COLUMNS}
        FROM dbo.punches
        ORDER BY fecha DESC, entrada DESC
        """,
        (limit,),
    )

    return {"count": len(rows), "items": rows}


@router.get("/columns")
def punches_columns(_user: dict = READ):
    _require_db()

    rows = fetch_all("""
        SELECT
            COLUMN_NAME,
            DATA_TYPE,
            IS_NULLABLE,
            CHARACTER_MAXIMUM_LENGTH
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = 'dbo'
          AND TABLE_NAME = 'punches'
        ORDER BY ORDINAL_POSITION
    """)

    return {"table": "dbo.punches", "columns": rows}


@router.get("/summary")
def punches_summary(_user: dict = READ):
    db = _require_db()

    return {
        **day_summary(TODAY),
        "date": "today",
        "database": db,
    }


@router.get("/ops-overview")
def ops_overview(_user: dict = READ):
    _require_db()

    return overview(
        day_summary(TODAY),
        day_summary(YESTERDAY),
    )


@router.get("/devices")
def punches_devices(_user: dict = READ):
    _require_db()

    rows = fetch_all("""
        SELECT
            LTRIM(RTRIM(dispositivo_origen)) AS dispositivo,
            COUNT(*) AS total_registros,
            COUNT(DISTINCT codigo) AS empleados,
            MAX(fecha) AS ultima_fecha
        FROM dbo.punches
        WHERE dispositivo_origen IS NOT NULL
          AND LTRIM(RTRIM(dispositivo_origen)) <> ''
        GROUP BY LTRIM(RTRIM(dispositivo_origen))
        ORDER BY total_registros DESC
    """)

    return {"count": len(rows), "items": rows}


@router.get("/employees")
def punches_employees(
    q: str = Query("", max_length=80),
    limit: int = Query(100, ge=1, le=500),
    _user: dict = READ,
):
    _require_db()

    params = [limit]
    where = ""

    if q.strip():
        where = "WHERE codigo LIKE ? OR ISNULL(nombre, '') LIKE ?"
        params.extend([
            f"%{q.strip()}%",
            f"%{q.strip()}%",
        ])

    rows = fetch_all(
        f"""
        SELECT TOP (?)
            codigo,
            MAX(nombre) AS nombre,
            MAX(departamento) AS departamento,
            COUNT(*) AS total_registros,
            MAX(fecha) AS ultima_fecha,
            MAX(dispositivo_origen) AS ultimo_dispositivo
        FROM dbo.punches
        {where}
        GROUP BY codigo
        ORDER BY MAX(fecha) DESC, codigo
        """,
        tuple(params),
    )

    return {"count": len(rows), "items": rows}


@router.get("/search")
def search_punches(
    limit: int = Query(100, ge=1, le=500),
    fecha: str | None = Query(None),
    dispositivo: str | None = Query(None),
    q: str = Query("", max_length=80),
    _user: dict = READ,
):
    _require_db()

    conditions = []
    params = [limit]

    if fecha:
        conditions.append("CAST(fecha AS date) = CAST(? AS date)")
        params.append(fecha)

    if dispositivo and dispositivo.strip().lower() != "todos":
        conditions.append("LTRIM(RTRIM(dispositivo_origen)) = ?")
        params.append(dispositivo.strip())

    if q.strip():
        conditions.append(
            "(codigo LIKE ? OR ISNULL(nombre, '') LIKE ?)"
        )

        params.extend([
            f"%{q.strip()}%",
            f"%{q.strip()}%",
        ])

    where = (
        "WHERE " + " AND ".join(conditions)
        if conditions
        else ""
    )

    rows = fetch_all(
        f"""
        SELECT TOP (?) {COLUMNS}
        FROM dbo.punches
        {where}
        ORDER BY fecha DESC, entrada DESC
        """,
        tuple(params),
    )

    try:
        append_sync(
            "consulta_ponches",
            "Filtro pantalla Ponches",
            len(rows),
            "ok",
        )
    except Exception:
        pass

    return {"count": len(rows), "items": rows}


@router.get("/stats")
def punches_stats(_user: dict = READ):
    _require_db()

    result = trends()

    result["by_device"] = fetch_all("""
        SELECT TOP 20
            LTRIM(RTRIM(dispositivo_origen)) AS name,
            COUNT(*) AS total
        FROM dbo.punches
        WHERE dispositivo_origen IS NOT NULL
          AND LTRIM(RTRIM(dispositivo_origen)) <> ''
        GROUP BY LTRIM(RTRIM(dispositivo_origen))
        ORDER BY total DESC
    """)

    result["activity"] = load_audit(40)

    return result


@router.get("/dashboard-combined")
def get_dashboard_combined(_user: dict = READ):
    db = _require_db()

    today = day_summary(TODAY)
    yesterday = day_summary(YESTERDAY)

    return {
        "summary": {
            **today,
            "date": "today",
            "database": db,
        },
        "overview": overview(today, yesterday),
        "stats": trends(),
        "recent": fetch_all(f"""
            SELECT TOP (10) {COLUMNS}
            FROM dbo.punches
            ORDER BY fecha DESC, entrada DESC
        """),
        "timestamp": datetime.now().strftime("%H:%M:%S"),
    }