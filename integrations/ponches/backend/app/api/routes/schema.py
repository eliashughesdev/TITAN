"""Historial SQL y consulta de tablas autorizadas."""

import logging
from datetime import date, timedelta

from fastapi import APIRouter, Depends, HTTPException, Query

from app.core.config import settings
from app.core.deps import require_permission
from app.services.audit import audit
from app.services.database import (
    fetch_all,
    get_connection,
    test_connection,
)

router = APIRouter(
    prefix="/schema",
    tags=["schema"],
    dependencies=[
        Depends(require_permission("settings.read")),
    ],
)

logger = logging.getLogger(__name__)


def _assert_db():
    db = test_connection()

    if db["status"] != "online":
        raise HTTPException(
            503,
            db.get("detail", "BD no disponible"),
        )


def _allowed_tables():
    configured = {
        table.lower()
        for table in settings.schema_allowlist
    }

    rows = fetch_all("""
        SELECT TABLE_NAME
        FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_SCHEMA = 'dbo'
          AND TABLE_TYPE = 'BASE TABLE'
    """)

    return {
        str(row["TABLE_NAME"])
        for row in rows
        if str(row["TABLE_NAME"]).lower()
        in (configured or {"punches"})
    }


@router.get("/tables")
def list_tables(
    user: dict = Depends(require_permission("settings.read")),
):
    _assert_db()

    names = sorted(_allowed_tables())

    audit(
        user["username"],
        "schema_tables",
        "dbo",
        {"count": len(names)},
    )

    return {"count": len(names), "tables": names}


@router.get("/columns/{table_name}")
def list_columns(
    table_name: str,
    user: dict = Depends(require_permission("settings.read")),
):
    _assert_db()

    if table_name not in _allowed_tables():
        raise HTTPException(400, "Tabla no permitida")

    columns = fetch_all(
        """
        SELECT
            COLUMN_NAME,
            DATA_TYPE,
            IS_NULLABLE,
            CHARACTER_MAXIMUM_LENGTH
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = 'dbo'
          AND TABLE_NAME = ?
        ORDER BY ORDINAL_POSITION
        """,
        (table_name,),
    )

    audit(
        user["username"],
        "schema_columns",
        table_name,
        {"count": len(columns)},
    )

    return {"table": table_name, "columns": columns}


@router.get("/preview")
def preview_table(
    table: str,
    limit: int = Query(20, ge=1, le=200),
    user: dict = Depends(require_permission("settings.read")),
):
    _assert_db()

    if table not in _allowed_tables():
        raise HTTPException(400, "Tabla no permitida")

    columns = fetch_all(
        """
        SELECT COLUMN_NAME
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = 'dbo'
          AND TABLE_NAME = ?
        ORDER BY ORDINAL_POSITION
        """,
        (table,),
    )

    safe_table = table.replace("]", "]]")

    rows = fetch_all(
        f"SELECT TOP ({int(limit)}) * FROM [dbo].[{safe_table}]"
    )

    audit(
        user["username"],
        "schema_preview",
        table,
        {"rows": len(rows)},
    )

    return {
        "columns": [
            column["COLUMN_NAME"]
            for column in columns
        ],
        "items": rows,
    }


@router.get("/analytics")
def sql_analytics(
    start: date | None = None,
    end: date | None = None,
    device: str = Query("", max_length=200),
    _user: dict = Depends(require_permission("settings.read")),
):
    if start and end and end < start:
        raise HTTPException(
            400,
            "La fecha final no puede ser anterior a la inicial",
        )

    if end == date.max:
        raise HTTPException(400, "Fecha final no válida")

    conn = None
    cursor = None

    try:
        conn = get_connection()
        conn.timeout = 30
        cursor = conn.cursor()

        def query(sql, params=()):
            if params:
                cursor.execute(sql, params)
            else:
                cursor.execute(sql)

            keys = [
                column[0]
                for column in cursor.description
            ]

            return [
                dict(zip(keys, row))
                for row in cursor.fetchall()
            ]

        expression = (
            "COALESCE("
            "NULLIF(LTRIM(RTRIM(dispositivo_origen)), ''), "
            "'Sin dispositivo')"
        )

        devices = query(f"""
            SELECT DISTINCT {expression} AS name
            FROM dbo.punches
            ORDER BY name
        """)

        device = device.strip()

        where = "WHERE fecha IS NOT NULL"
        params = []

        if start:
            where += " AND fecha >= ?"
            params.append(start)

        if end:
            where += " AND fecha < ?"
            params.append(end + timedelta(days=1))

        if device:
            where += f" AND {expression} = ?"
            params.append(device)

        # Crear la tabla en un comando SIN parámetros.
        # Así permanece disponible en esta conexión después
        # de ejecutar el INSERT parametrizado.
        cursor.execute(f"""
            SELECT TOP (0)
                codigo,
                CAST(fecha AS date) AS dia,
                entrada,
                salida,
                {expression} AS reloj
            INTO #history
            FROM dbo.punches
        """)

        insert_sql = f"""
            INSERT INTO #history (
                codigo,
                dia,
                entrada,
                salida,
                reloj
            )
            SELECT
                codigo,
                CAST(fecha AS date),
                entrada,
                salida,
                {expression}
            FROM dbo.punches
            {where}
        """

        if params:
            cursor.execute(insert_sql, tuple(params))
        else:
            cursor.execute(insert_sql)

        bounds = query("""
            SELECT
                MIN(dia) AS first_date,
                MAX(dia) AS last_date
            FROM #history
        """)[0]

        first = bounds["first_date"]
        last = bounds["last_date"]

        monthly = bool(
            first and last and (last - first).days > 120
        )

        summary = query("""
            SELECT
                COUNT(*) AS [rows],
                COUNT(DISTINCT codigo) AS employees,
                COUNT(DISTINCT reloj) AS devices,
                COALESCE(SUM(
                    CASE WHEN entrada IS NOT NULL
                    THEN 1 ELSE 0 END
                ), 0) AS entries,
                COALESCE(SUM(
                    CASE WHEN salida IS NOT NULL
                    THEN 1 ELSE 0 END
                ), 0) AS exits,
                COALESCE(SUM(
                    CASE
                        WHEN entrada IS NOT NULL
                         AND salida IS NOT NULL
                        THEN 1 ELSE 0
                    END
                ), 0) AS complete,
                COALESCE(SUM(
                    CASE
                        WHEN entrada IS NOT NULL
                         AND salida IS NULL
                        THEN 1 ELSE 0
                    END
                ), 0) AS entry_only,
                COALESCE(SUM(
                    CASE
                        WHEN entrada IS NULL
                         AND salida IS NOT NULL
                        THEN 1 ELSE 0
                    END
                ), 0) AS exit_only,
                COALESCE(SUM(
                    CASE
                        WHEN entrada IS NULL
                         AND salida IS NULL
                        THEN 1 ELSE 0
                    END
                ), 0) AS empty
            FROM #history
        """)[0]

        by_device = query("""
            SELECT
                reloj AS name,
                COUNT(*) AS [rows],
                SUM(
                    CASE WHEN entrada IS NOT NULL
                    THEN 1 ELSE 0 END
                ) AS entries,
                SUM(
                    CASE WHEN salida IS NOT NULL
                    THEN 1 ELSE 0 END
                ) AS exits
            FROM #history
            GROUP BY reloj
            ORDER BY [rows] DESC, reloj
        """)

        period_expression = (
            "CONVERT(varchar(7), dia, 23)"
            if monthly
            else "CONVERT(varchar(10), dia, 23)"
        )

        by_day = query(f"""
            SELECT
                {period_expression} AS [day],
                SUM(
                    CASE WHEN entrada IS NOT NULL
                    THEN 1 ELSE 0 END
                ) AS entries,
                SUM(
                    CASE WHEN salida IS NOT NULL
                    THEN 1 ELSE 0 END
                ) AS exits
            FROM #history
            GROUP BY {period_expression}
            ORDER BY [day]
        """)

        by_hour = query("""
            SELECT
                [hour],
                SUM(entries) AS entries,
                SUM(exits) AS exits
            FROM (
                SELECT
                    DATEPART(hour, entrada) AS [hour],
                    1 AS entries,
                    0 AS exits
                FROM #history
                WHERE entrada IS NOT NULL

                UNION ALL

                SELECT
                    DATEPART(hour, salida),
                    0,
                    1
                FROM #history
                WHERE salida IS NOT NULL
            ) h
            GROUP BY [hour]
            ORDER BY [hour]
        """)

        return {
            "summary": summary,
            "devices": devices,
            "by_device": by_device,
            "by_day": by_day,
            "by_hour": by_hour,
            "period": {
                "start": first.isoformat() if first else None,
                "end": last.isoformat() if last else None,
                "device": device,
                "granularity": "month" if monthly else "day",
                "global": start is None and end is None,
            },
        }

    except Exception as exc:
        logger.exception("No se pudo calcular el historial SQL")

        raise HTTPException(
            503,
            "No se pudo consultar BioTimeDB. "
            "Revisa el registro de Python para identificar el error SQL.",
        ) from exc

    finally:
        if cursor is not None:
            cursor.close()

        if conn is not None:
            conn.close()