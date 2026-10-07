from __future__ import annotations

import hmac
from datetime import datetime, timezone

from fastapi import (
    APIRouter,
    Header,
    HTTPException,
    Query,
)

from app.core.config import settings
from app.services.database import (
    fetch_all,
    test_connection,
)


router = APIRouter(
    prefix="/internal/titan",
    tags=["TitanMDM integration"],
)


# ================================================================
# SECURITY
# ================================================================

def authorize(
    key: str | None,
) -> None:
    expected = (
        settings
        .TITAN_PONCHES_INTEGRATION_KEY
        .strip()
    )

    if len(expected) < 32:
        raise HTTPException(
            status_code=503,
            detail=(
                "Integración TitanMDM "
                "no configurada."
            ),
        )

    if (
        not key
        or
        not hmac.compare_digest(
            key,
            expected,
        )
    ):
        raise HTTPException(
            status_code=401,
            detail=(
                "Credencial de integración "
                "inválida."
            ),
        )


def require_database() -> None:
    result = test_connection()

    if (
        result.get("status")
        !=
        "online"
    ):
        raise HTTPException(
            status_code=503,
            detail=(
                "BioTimeDB no está "
                "disponible."
            ),
        )


# ================================================================
# HELPERS
# ================================================================

def integer(
    value,
) -> int:
    try:
        return int(
            value or 0
        )
    except (
        TypeError,
        ValueError,
    ):
        return 0


def recent(
    limit: int,
) -> list[dict]:
    return fetch_all(
        """
        SELECT TOP (?)
            id,
            codigo,
            nombre,
            departamento,
            fecha,
            entrada,
            salida,
            dispositivo_origen
        FROM dbo.punches
        ORDER BY
            fecha DESC,
            entrada DESC,
            salida DESC,
            id DESC
        """,
        (
            limit,
        ),
    )


# ================================================================
# HEALTH
# ================================================================

@router.get("/health")
def integration_health(
    x_titan_integration_key: str | None =
        Header(
            default=None,
            alias="X-Titan-Integration-Key",
        ),
):
    authorize(
        x_titan_integration_key
    )

    database = test_connection()

    return {
        "service": "ponches",
        "status": "online",
        "database":
            database.get(
                "status"
            ),
        "databaseName":
            settings.DB_NAME,
        "server":
            settings.DB_SERVER,
        "generatedAtUtc":
            datetime
            .now(
                timezone.utc
            )
            .isoformat(),
    }


# ================================================================
# DASHBOARD
# ================================================================

@router.get("/dashboard")
def dashboard(
    x_titan_integration_key: str | None =
        Header(
            default=None,
            alias="X-Titan-Integration-Key",
        ),
):
    authorize(
        x_titan_integration_key
    )

    require_database()

    try:

        # ========================================================
        # HOY
        #
        # IMPORTANTE:
        # una fila puede tener:
        #
        # entrada
        # salida
        #
        # Por tanto:
        #
        # COUNT(*) NO equivale necesariamente
        # al número real de marcas.
        # ========================================================

        today_rows = fetch_all(
            """
            SELECT
                COUNT(*) AS registros_hoy,

                COUNT(
                    DISTINCT codigo
                ) AS empleados_hoy,

                COUNT(
                    DISTINCT
                    NULLIF(
                        LTRIM(
                            RTRIM(
                                dispositivo_origen
                            )
                        ),
                        ''
                    )
                ) AS relojes_hoy,

                SUM(
                    CASE
                        WHEN entrada IS NOT NULL
                        THEN 1
                        ELSE 0
                    END
                ) AS entradas_hoy,

                SUM(
                    CASE
                        WHEN salida IS NOT NULL
                        THEN 1
                        ELSE 0
                    END
                ) AS salidas_hoy,

                SUM(
                    CASE
                        WHEN
                            entrada IS NOT NULL
                            AND
                            salida IS NULL
                        THEN 1
                        ELSE 0
                    END
                ) AS sin_salida

            FROM dbo.punches

            WHERE
                fecha >=
                    CAST(
                        GETDATE()
                        AS date
                    )

                AND

                fecha <
                    DATEADD(
                        day,
                        1,
                        CAST(
                            GETDATE()
                            AS date
                        )
                    )
            """
        )

        today = (
            today_rows[0]
            if today_rows
            else {}
        )

        entradas_hoy = integer(
            today.get(
                "entradas_hoy"
            )
        )

        salidas_hoy = integer(
            today.get(
                "salidas_hoy"
            )
        )

        total_marcas_hoy = (
            entradas_hoy
            +
            salidas_hoy
        )

        # ========================================================
        # AYER
        # ========================================================

        yesterday_rows = fetch_all(
            """
            SELECT
                COUNT(*) AS registros_ayer,

                COUNT(
                    DISTINCT codigo
                ) AS empleados_ayer,

                SUM(
                    CASE
                        WHEN entrada IS NOT NULL
                        THEN 1
                        ELSE 0
                    END
                ) AS entradas_ayer,

                SUM(
                    CASE
                        WHEN salida IS NOT NULL
                        THEN 1
                        ELSE 0
                    END
                ) AS salidas_ayer

            FROM dbo.punches

            WHERE
                fecha >=
                    DATEADD(
                        day,
                        -1,
                        CAST(
                            GETDATE()
                            AS date
                        )
                    )

                AND

                fecha <
                    CAST(
                        GETDATE()
                        AS date
                    )
            """
        )

        yesterday = (
            yesterday_rows[0]
            if yesterday_rows
            else {}
        )

        entradas_ayer = integer(
            yesterday.get(
                "entradas_ayer"
            )
        )

        salidas_ayer = integer(
            yesterday.get(
                "salidas_ayer"
            )
        )

        total_marcas_ayer = (
            entradas_ayer
            +
            salidas_ayer
        )

        # ========================================================
        # ACTIVIDAD POR HORA
        #
        # Cada entrada y salida se considera
        # un evento biométrico independiente.
        # ========================================================

        by_hour = fetch_all(
            """
            SELECT
                DATEPART(
                    HOUR,
                    marcas.marca
                ) AS hora,

                COUNT(*) AS total

            FROM dbo.punches p

            CROSS APPLY
            (
                VALUES
                    (p.entrada),
                    (p.salida)
            ) marcas(marca)

            WHERE
                p.fecha >=
                    CAST(
                        GETDATE()
                        AS date
                    )

                AND

                p.fecha <
                    DATEADD(
                        day,
                        1,
                        CAST(
                            GETDATE()
                            AS date
                        )
                    )

                AND

                marcas.marca
                    IS NOT NULL

            GROUP BY
                DATEPART(
                    HOUR,
                    marcas.marca
                )

            ORDER BY
                DATEPART(
                    HOUR,
                    marcas.marca
                )
            """
        )

        # ========================================================
        # ACTIVIDAD ÚLTIMOS 14 DÍAS
        # ========================================================

        by_day = fetch_all(
            """
            SELECT
                CONVERT(
                    varchar(10),
                    CAST(
                        fecha AS date
                    ),
                    23
                ) AS label,

                SUM(
                    CASE
                        WHEN entrada IS NOT NULL
                        THEN 1
                        ELSE 0
                    END
                )
                +
                SUM(
                    CASE
                        WHEN salida IS NOT NULL
                        THEN 1
                        ELSE 0
                    END
                ) AS total

            FROM dbo.punches

            WHERE
                fecha >=
                    DATEADD(
                        day,
                        -13,
                        CAST(
                            GETDATE()
                            AS date
                        )
                    )

            GROUP BY
                CAST(
                    fecha AS date
                )

            ORDER BY
                CAST(
                    fecha AS date
                )
            """
        )

        # ========================================================
        # DEPARTAMENTOS
        # ========================================================

        by_department = fetch_all(
            """
            SELECT TOP (10)

                ISNULL(
                    NULLIF(
                        LTRIM(
                            RTRIM(
                                departamento
                            )
                        ),
                        ''
                    ),
                    'Sin departamento'
                ) AS label,

                SUM(
                    CASE
                        WHEN entrada IS NOT NULL
                        THEN 1
                        ELSE 0
                    END
                )
                +
                SUM(
                    CASE
                        WHEN salida IS NOT NULL
                        THEN 1
                        ELSE 0
                    END
                ) AS total

            FROM dbo.punches

            WHERE
                fecha >=
                    CAST(
                        GETDATE()
                        AS date
                    )

                AND

                fecha <
                    DATEADD(
                        day,
                        1,
                        CAST(
                            GETDATE()
                            AS date
                        )
                    )

            GROUP BY
                ISNULL(
                    NULLIF(
                        LTRIM(
                            RTRIM(
                                departamento
                            )
                        ),
                        ''
                    ),
                    'Sin departamento'
                )

            ORDER BY
                total DESC
            """
        )

        # ========================================================
        # RELOJ / DISPOSITIVO
        # ========================================================

        by_device = fetch_all(
            """
            SELECT TOP (20)

                ISNULL(
                    NULLIF(
                        LTRIM(
                            RTRIM(
                                dispositivo_origen
                            )
                        ),
                        ''
                    ),
                    'Sin identificar'
                ) AS label,

                SUM(
                    CASE
                        WHEN entrada IS NOT NULL
                        THEN 1
                        ELSE 0
                    END
                )
                +
                SUM(
                    CASE
                        WHEN salida IS NOT NULL
                        THEN 1
                        ELSE 0
                    END
                ) AS total

            FROM dbo.punches

            WHERE
                fecha >=
                    CAST(
                        GETDATE()
                        AS date
                    )

                AND

                fecha <
                    DATEADD(
                        day,
                        1,
                        CAST(
                            GETDATE()
                            AS date
                        )
                    )

            GROUP BY
                ISNULL(
                    NULLIF(
                        LTRIM(
                            RTRIM(
                                dispositivo_origen
                            )
                        ),
                        ''
                    ),
                    'Sin identificar'
                )

            ORDER BY
                total DESC
            """
        )

        # ========================================================
        # RESPONSE CONTRACT
        #
        # Mantener nombres consistentes y explícitos.
        # ========================================================

        return {
            "generatedAtUtc":
                datetime
                .now(
                    timezone.utc
                )
                .isoformat(),

            "summary": {
                "totalPunchesToday":
                    total_marcas_hoy,

                "totalPunchesYesterday":
                    total_marcas_ayer,

                "recordsToday":
                    integer(
                        today.get(
                            "registros_hoy"
                        )
                    ),

                "employeesToday":
                    integer(
                        today.get(
                            "empleados_hoy"
                        )
                    ),

                "employeesYesterday":
                    integer(
                        yesterday.get(
                            "empleados_ayer"
                        )
                    ),

                "devicesToday":
                    integer(
                        today.get(
                            "relojes_hoy"
                        )
                    ),

                "entriesToday":
                    entradas_hoy,

                "exitsToday":
                    salidas_hoy,

                "openShifts":
                    integer(
                        today.get(
                            "sin_salida"
                        )
                    ),
            },

            "byHour": [
                {
                    "hour":
                        integer(
                            item.get(
                                "hora"
                            )
                        ),

                    "total":
                        integer(
                            item.get(
                                "total"
                            )
                        ),
                }
                for item
                in by_hour
            ],

            "byDay": [
                {
                    "label":
                        str(
                            item.get(
                                "label"
                            )
                            or
                            ""
                        ),

                    "total":
                        integer(
                            item.get(
                                "total"
                            )
                        ),
                }
                for item
                in by_day
            ],

            "byDepartment": [
                {
                    "label":
                        str(
                            item.get(
                                "label"
                            )
                            or
                            "Sin departamento"
                        ),

                    "total":
                        integer(
                            item.get(
                                "total"
                            )
                        ),
                }
                for item
                in by_department
            ],

            "byDevice": [
                {
                    "label":
                        str(
                            item.get(
                                "label"
                            )
                            or
                            "Sin identificar"
                        ),

                    "total":
                        integer(
                            item.get(
                                "total"
                            )
                        ),
                }
                for item
                in by_device
            ],

            "recent":
                recent(
                    30
                ),
        }

    except Exception as exc:
        raise HTTPException(
            status_code=503,
            detail=(
                "No se pudo consultar "
                "el dashboard de BioTime."
            ),
        ) from exc


# ================================================================
# RECORDS
# ================================================================

@router.get("/records")
def records(
    limit: int =
        Query(
            default=100,
            ge=1,
            le=200,
        ),

    search: str =
        Query(
            default="",
            max_length=80,
        ),

    x_titan_integration_key: str | None =
        Header(
            default=None,
            alias="X-Titan-Integration-Key",
        ),
):

    authorize(
        x_titan_integration_key
    )

    require_database()

    term = search.strip()

    try:
        if not term:

            items = recent(
                limit
            )

        else:

            pattern = (
                f"%{term}%"
            )

            items = fetch_all(
                """
                SELECT TOP (?)
                    id,
                    codigo,
                    nombre,
                    departamento,
                    fecha,
                    entrada,
                    salida,
                    dispositivo_origen

                FROM dbo.punches

                WHERE
                    codigo LIKE ?
                    OR
                    nombre LIKE ?
                    OR
                    departamento LIKE ?
                    OR
                    dispositivo_origen LIKE ?

                ORDER BY
                    fecha DESC,
                    entrada DESC,
                    salida DESC,
                    id DESC
                """,
                (
                    limit,
                    pattern,
                    pattern,
                    pattern,
                    pattern,
                ),
            )

        return {
            "items":
                items,

            "count":
                len(
                    items
                ),

            "limit":
                limit,

            "search":
                term,
        }

    except Exception as exc:
        raise HTTPException(
            status_code=503,
            detail=(
                "No se pudieron consultar "
                "los registros."
            ),
        ) from exc


# ================================================================
# COLLABORATORS
# ================================================================

@router.get("/collaborators")
def collaborators(
    limit: int =
        Query(
            default=100,
            ge=1,
            le=200,
        ),

    search: str =
        Query(
            default="",
            max_length=80,
        ),

    x_titan_integration_key: str | None =
        Header(
            default=None,
            alias="X-Titan-Integration-Key",
        ),
):

    authorize(
        x_titan_integration_key
    )

    require_database()

    term = search.strip()

    where = ""

    params: list[object] = [
        limit,
    ]

    if term:
        where = """
        WHERE
            codigo LIKE ?
            OR
            nombre LIKE ?
            OR
            departamento LIKE ?
        """

        pattern = (
            f"%{term}%"
        )

        params.extend(
            (
                pattern,
                pattern,
                pattern,
            )
        )

    try:
        items = fetch_all(
            f"""
            SELECT TOP (?)
                codigo,
                MAX(nombre)
                    AS nombre,
                MAX(departamento)
                    AS departamento,
                COUNT(*)
                    AS registros,
                MAX(fecha)
                    AS ultima_fecha

            FROM dbo.punches

            {where}

            GROUP BY
                codigo

            ORDER BY
                MAX(fecha) DESC
            """,
            tuple(
                params
            ),
        )

        return {
            "items":
                items,

            "count":
                len(
                    items
                ),
        }

    except Exception as exc:
        raise HTTPException(
            status_code=503,
            detail=(
                "No se pudieron consultar "
                "los colaboradores."
            ),
        ) from exc


# ================================================================
# DEVICES
# ================================================================

@router.get("/devices")
def devices(
    x_titan_integration_key: str | None =
        Header(
            default=None,
            alias="X-Titan-Integration-Key",
        ),
):

    authorize(
        x_titan_integration_key
    )

    require_database()

    try:
        items = fetch_all(
            """
            SELECT TOP (100)

                dispositivo_origen
                    AS nombre,

                COUNT(*)
                    AS registros,

                COUNT(
                    DISTINCT codigo
                )
                    AS colaboradores,

                MAX(fecha)
                    AS ultima_fecha

            FROM dbo.punches

            WHERE
                dispositivo_origen
                    IS NOT NULL

                AND

                LTRIM(
                    RTRIM(
                        dispositivo_origen
                    )
                ) <> ''

            GROUP BY
                dispositivo_origen

            ORDER BY
                MAX(fecha) DESC
            """
        )

        return {
            "items":
                items,

            "count":
                len(
                    items
                ),
        }

    except Exception as exc:
        raise HTTPException(
            status_code=503,
            detail=(
                "No se pudo consultar "
                "la actividad de los relojes."
            ),
        ) from exc


# ================================================================
# LEGACY ORIGINAL DASHBOARD
# ================================================================

@router.get(
    "/dashboard-original"
)
def dashboard_original(
    x_titan_integration_key: str | None =
        Header(
            default=None,
            alias="X-Titan-Integration-Key",
        ),
):

    authorize(
        x_titan_integration_key
    )

    require_database()

    from app.api.routes.records_query import (
        get_dashboard_combined,
    )

    return get_dashboard_combined(
        _user={
            "username":
                "titan-internal"
        }
    )


# ================================================================
# DEVICE HEALTH
# ================================================================

@router.get(
    "/device-health"
)
def device_health_original(
    x_titan_integration_key: str | None =
        Header(
            default=None,
            alias="X-Titan-Integration-Key",
        ),
):

    authorize(
        x_titan_integration_key
    )

    from app.api.routes.records_clocks import (
        device_health,
    )

    return device_health(
        _user={
            "username":
                "titan-internal"
        }
    )