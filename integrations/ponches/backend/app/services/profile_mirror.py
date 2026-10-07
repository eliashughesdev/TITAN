from __future__ import annotations

import json
from datetime import datetime
from threading import RLock
from uuid import uuid4

from fastapi import HTTPException

from app.services import clock_mirror
from app.services.json_store import (
    data_path,
    read_json,
    write_json,
)

FILE = data_path("collaborators.json")
_lock = RLock()


def initialize(connection) -> None:
    connection.execute("""
        CREATE TABLE IF NOT EXISTS profile_outbox (
            id TEXT PRIMARY KEY,
            organization TEXT NOT NULL,
            actor TEXT NOT NULL,
            codigo TEXT NOT NULL,
            nombre TEXT NOT NULL,
            devices TEXT NOT NULL,
            status TEXT NOT NULL,
            created_at TEXT NOT NULL,
            detail TEXT NOT NULL DEFAULT '',
            payload TEXT NOT NULL DEFAULT '{}'
        )
    """)

    columns = {
        row[1]
        for row in connection.execute(
            "PRAGMA table_info(profile_outbox)"
        )
    }

    if "payload" not in columns:
        connection.execute("""
            ALTER TABLE profile_outbox
            ADD COLUMN payload TEXT
            NOT NULL DEFAULT '{}'
        """)

    connection.commit()


def _assigned(profile: dict) -> list[str]:
    result = list(
        dict.fromkeys(
            str(name).strip()
            for name in (
                profile.get("dispositivos")
                or []
            )
            if str(name).strip()
        )
    )

    office = str(
        profile.get("reloj_oficina")
        or ""
    ).strip()

    if office and office not in result:
        result.append(office)

    return result


def _full_payload(
    profile: dict,
) -> dict:
    return {
        "nombre": (
            profile.get("nombre_reloj")
            or profile.get("nombre")
            or ""
        ),
        "password_device": (
            profile.get("password_device")
            or ""
        ),
        "card_no": (
            profile.get("card_no")
            or profile.get("rfid")
            or ""
        ),
        "privilege": (
            profile.get("privilege")
            or 0
        ),
    }


def _changed_payload(
    previous: dict,
    current: dict,
) -> dict:
    desired = _full_payload(current)

    if not previous:
        return desired

    old = _full_payload(previous)

    return {
        key: value
        for key, value in desired.items()
        if str(value)
        != str(old.get(key, ""))
    }


def flush() -> None:
    with (
        _lock,
        clock_mirror.database()
        as connection,
    ):
        initialize(connection)

        rows = connection.execute("""
            SELECT *
            FROM profile_outbox
            WHERE status = 'prepared'
            ORDER BY created_at
            LIMIT 20
        """).fetchall()

        profiles = read_json(
            FILE,
            [],
        )

        by_code = {
            str(item.get("codigo")): item
            for item in profiles
            if isinstance(item, dict)
        }

        for row in rows:
            profile = by_code.get(
                row["codigo"],
                {},
            )

            if (
                profile.get("_mirror_revision")
                != row["id"]
            ):
                continue

            try:
                payload = json.loads(
                    row["payload"] or "{}"
                )
            except json.JSONDecodeError:
                payload = {
                    "nombre": row["nombre"],
                }

            for device in json.loads(
                row["devices"]
            ):
                clock_mirror.enqueue(
                    row["organization"],
                    row["actor"],
                    device,
                    row["codigo"],
                    row["nombre"],
                    payload,
                )

            connection.execute("""
                UPDATE profile_outbox
                SET status = 'published'
                WHERE id = ?
            """, (
                row["id"],
            ))

            connection.commit()


def save_profile(
    payload: dict,
    user: dict,
) -> dict:
    codigo = str(
        payload.get("codigo")
        or ""
    ).strip()

    if not codigo:
        raise HTTPException(
            400,
            "Código requerido",
        )

    with _lock:
        profiles = read_json(
            FILE,
            [],
        )

        if not isinstance(
            profiles,
            list,
        ):
            raise HTTPException(
                500,
                "El archivo de colaboradores "
                "tiene formato incorrecto",
            )

        previous = next(
            (
                item
                for item in profiles
                if str(
                    item.get("codigo")
                )
                == codigo
            ),
            {},
        )

        entry = {
            **previous,
            **payload,
            "codigo": codigo,
        }

        assigned = _assigned(entry)
        old_assigned = set(
            _assigned(previous)
        )

        new_devices = [
            name
            for name in assigned
            if name not in old_assigned
        ]

        changes = _changed_payload(
            previous,
            entry,
        )

        targets = (
            assigned
            if changes
            else new_devices
        )

        sync_payload = (
            changes
            or (
                _full_payload(entry)
                if new_devices
                else {}
            )
        )

        with (
            clock_mirror.database()
            as connection
        ):
            initialize(connection)

            waiting = connection.execute("""
                SELECT devices
                FROM profile_outbox
                WHERE codigo = ?
                  AND organization = ?
                  AND status = 'prepared'
            """, (
                codigo,
                user.get(
                    "organization_id",
                    "",
                ),
            )).fetchall()

        for row in waiting:
            for name in json.loads(
                row["devices"]
            ):
                if (
                    name in assigned
                    and name not in targets
                ):
                    targets.append(name)

        if targets:
            if (
                user.get("source")
                != "titan"
                or "collaborators.sync"
                not in user.get(
                    "operations",
                    [],
                )
            ):
                raise HTTPException(
                    403,
                    "Guardar estos cambios requiere "
                    "permiso de sincronización "
                    "de colaboradores",
                )

            try:
                sync_payload = (
                    clock_mirror.normalize_profile(
                        codigo,
                        sync_payload,
                    )
                )
            except (
                ValueError,
                TypeError,
            ) as error:
                raise HTTPException(
                    400,
                    str(error),
                )

        revision = str(uuid4())
        stamp = datetime.now().strftime(
            "%Y-%m-%d %H:%M:%S"
        )

        entry.update({
            "actualizado": stamp,
            "creado": previous.get(
                "creado",
                stamp,
            ),
            "_mirror_revision": revision,
        })

        with (
            clock_mirror.database()
            as connection
        ):
            initialize(connection)

            connection.execute("""
                UPDATE profile_outbox
                SET status = 'superseded'
                WHERE codigo = ?
                  AND organization = ?
                  AND status = 'prepared'
            """, (
                codigo,
                user.get(
                    "organization_id",
                    "",
                ),
            ))

            if targets:
                connection.execute("""
                    INSERT INTO profile_outbox (
                        id,
                        organization,
                        actor,
                        codigo,
                        nombre,
                        devices,
                        status,
                        created_at,
                        payload
                    )
                    VALUES (
                        ?, ?, ?, ?, ?, ?,
                        'prepared', ?, ?
                    )
                """, (
                    revision,
                    user["organization_id"],
                    user["username"],
                    codigo,
                    str(
                        entry.get(
                            "nombre_reloj"
                        )
                        or entry.get("nombre")
                        or ""
                    ),
                    json.dumps(targets),
                    clock_mirror.now(),
                    json.dumps(
                        sync_payload,
                        ensure_ascii=False,
                    ),
                ))

            connection.commit()

        profiles = [
            item
            for item in profiles
            if str(item.get("codigo"))
            != codigo
        ]

        profiles.append(entry)

        write_json(
            FILE,
            profiles,
        )

        status = (
            "pending"
            if targets
            else "not_required"
        )

        if targets:
            try:
                flush()
            except Exception:
                status = "prepared"

        changed = (
            [
                key
                for key in sync_payload
                if key != "codigo"
            ]
            if targets
            else []
        )

        message = (
            "Ficha guardada. Los cambios se "
            "enviarán y verificarán automáticamente "
            "en los relojes asignados."
            if targets
            else
            "Ficha guardada. No hay cambios físicos "
            "pendientes para los relojes."
        )

        return {
            **entry,
            "mirror": {
                "status": status,
                "devices": targets,
                "fields": changed,
                "confirmed": False,
            },
            "message": message,
        }


def pending_history(
    organization: str,
) -> list[dict]:
    with (
        clock_mirror.database()
        as connection
    ):
        initialize(connection)

        rows = connection.execute("""
            SELECT *
            FROM profile_outbox
            WHERE organization = ?
              AND status = 'prepared'
            ORDER BY created_at DESC
            LIMIT 100
        """, (
            organization,
        )).fetchall()

    result: list[dict] = []

    for row in rows:
        try:
            payload = json.loads(
                row["payload"] or "{}"
            )
        except json.JSONDecodeError:
            payload = {}

        for name in json.loads(
            row["devices"]
        ):
            result.append({
                "id": (
                    row["id"]
                    + ":"
                    + name
                ),
                "device": name,
                "codigo": row["codigo"],
                "nombre": row["nombre"],
                "status": "prepared",
                "attempts": 0,
                "created_at": (
                    row["created_at"]
                ),
                "updated_at": (
                    row["created_at"]
                ),
                "fields": [
                    key
                    for key in payload
                    if key != "codigo"
                ],
                "detail": (
                    "Cambio guardado; pendiente "
                    "de publicar en la cola física"
                ),
            })

    return result