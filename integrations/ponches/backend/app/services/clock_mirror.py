from __future__ import annotations

import json
import sqlite3
import time
from contextlib import contextmanager
from datetime import datetime, timezone
from uuid import uuid4

from app.services.json_store import data_path

FILE = data_path("clock_mirror.sqlite3")
MAX_ATTEMPTS = 6


class ClockConflict(Exception):
    pass


@contextmanager
def database():
    connection = sqlite3.connect(FILE, timeout=20)
    connection.row_factory = sqlite3.Row

    try:
        connection.execute("PRAGMA journal_mode=WAL")
        connection.execute("""
            CREATE TABLE IF NOT EXISTS mirror_jobs (
                id TEXT PRIMARY KEY,
                organization TEXT NOT NULL,
                actor TEXT NOT NULL,
                device TEXT NOT NULL,
                codigo TEXT NOT NULL,
                nombre TEXT NOT NULL,
                status TEXT NOT NULL,
                attempts INTEGER NOT NULL DEFAULT 0,
                next_at REAL NOT NULL,
                lease_until REAL NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                detail TEXT NOT NULL DEFAULT '',
                result TEXT NOT NULL DEFAULT '{}',
                payload TEXT NOT NULL DEFAULT '{}'
            )
        """)

        columns = {
            row[1]
            for row in connection.execute(
                "PRAGMA table_info(mirror_jobs)"
            )
        }

        if "payload" not in columns:
            connection.execute("""
                ALTER TABLE mirror_jobs
                ADD COLUMN payload TEXT NOT NULL DEFAULT '{}'
            """)

        connection.commit()
        yield connection

    finally:
        connection.close()


def now() -> str:
    return datetime.now(timezone.utc).isoformat()


def validate_name(value: object) -> str:
    name = str(value or "").strip()

    if not name or any(ord(char) < 32 for char in name):
        raise ValueError(
            "Nombre vacío o con caracteres de control"
        )

    if len(name.encode("utf-8")) > 24:
        raise ValueError(
            "El nombre admite un máximo de 24 bytes UTF-8; "
            "utiliza una abreviatura"
        )

    return name


def normalize_profile(
    codigo: object,
    values: dict | None,
) -> dict:
    code = str(codigo or "").strip()

    if not code.isascii() or not code.isdigit():
        raise ValueError(
            "El código del reloj debe contener solo números"
        )

    source = values or {}
    result: dict = {
        "codigo": code,
    }

    if "nombre" in source or "nombre_reloj" in source:
        result["nombre"] = validate_name(
            source.get("nombre_reloj")
            or source.get("nombre")
        )

    if (
        "password_device" in source
        or "password" in source
    ):
        password = str(
            source.get(
                "password_device",
                source.get("password", ""),
            )
            or ""
        )

        if len(password.encode("utf-8")) > 8:
            raise ValueError(
                "El PIN del reloj admite un máximo de 8 bytes"
            )

        result["password"] = password

    if (
        "card_no" in source
        or "rfid" in source
        or "card" in source
    ):
        raw = str(
            source.get("card_no")
            or source.get("rfid")
            or source.get("card")
            or "0"
        ).strip()

        if raw and not raw.isdigit():
            raise ValueError(
                "La tarjeta debe contener solo números"
            )

        result["card"] = int(raw or 0)

    if "privilege" in source:
        privilege = int(
            source.get("privilege") or 0
        )

        if privilege not in (0, 14):
            raise ValueError(
                "Privilegio inválido; usa 0 para usuario "
                "o 14 para administrador"
            )

        result["privilege"] = privilege

    if "group_id" in source:
        result["group_id"] = str(
            source.get("group_id") or "1"
        )

    return result


def enqueue(
    organization: str,
    actor: str,
    device: str,
    codigo: str,
    nombre: str = "",
    payload: dict | None = None,
) -> dict:
    values = dict(payload or {})

    if (
        nombre
        and "nombre" not in values
        and "nombre_reloj" not in values
    ):
        values["nombre"] = nombre

    desired = normalize_profile(
        codigo,
        values,
    )

    shown_name = str(
        desired.get("nombre")
        or nombre
        or ""
    )

    job_id = str(uuid4())
    stamp = now()

    with database() as connection:
        connection.execute("BEGIN IMMEDIATE")

        connection.execute("""
            UPDATE mirror_jobs
            SET
                status = 'superseded',
                updated_at = ?
            WHERE organization = ?
              AND device = ?
              AND codigo = ?
              AND status IN ('pending', 'retry')
        """, (
            stamp,
            organization,
            device,
            str(codigo),
        ))

        connection.execute("""
            INSERT INTO mirror_jobs (
                id,
                organization,
                actor,
                device,
                codigo,
                nombre,
                payload,
                status,
                next_at,
                created_at,
                updated_at
            )
            VALUES (
                ?, ?, ?, ?, ?, ?, ?,
                'pending', ?, ?, ?
            )
        """, (
            job_id,
            organization,
            actor,
            device,
            str(codigo),
            shown_name,
            json.dumps(
                desired,
                ensure_ascii=False,
            ),
            time.time(),
            stamp,
            stamp,
        ))

        connection.commit()

    return {
        "id": job_id,
        "device": device,
        "status": "pending",
        "fields": sorted(desired.keys()),
    }


def history(
    organization: str,
) -> list[dict]:
    with database() as connection:
        rows = connection.execute("""
            SELECT
                id,
                device,
                codigo,
                nombre,
                status,
                attempts,
                created_at,
                updated_at,
                detail,
                result,
                payload
            FROM mirror_jobs
            WHERE organization = ?
            ORDER BY created_at DESC
            LIMIT 100
        """, (
            organization,
        )).fetchall()

    items: list[dict] = []

    for row in rows:
        item = dict(row)

        for field in ("result", "payload"):
            try:
                item[field] = json.loads(
                    item[field] or "{}"
                )
            except (
                TypeError,
                json.JSONDecodeError,
            ):
                item[field] = {}

        item["fields"] = [
            key
            for key in item["payload"]
            if key != "codigo"
        ]

        items.append(item)

    return items


def claim() -> dict | None:
    current = time.time()

    with database() as connection:
        connection.execute(
            "BEGIN IMMEDIATE"
        )

        connection.execute("""
            UPDATE mirror_jobs
            SET
                status = 'retry',
                next_at = ?,
                lease_until = 0
            WHERE status = 'running'
              AND lease_until < ?
        """, (
            current,
            current,
        ))

        row = connection.execute("""
            SELECT *
            FROM mirror_jobs AS candidate
            WHERE candidate.status
                  IN ('pending', 'retry')
              AND candidate.next_at <= ?
              AND NOT EXISTS (
                  SELECT 1
                  FROM mirror_jobs AS busy
                  WHERE busy.device =
                        candidate.device
                    AND busy.status =
                        'running'
              )
            ORDER BY candidate.created_at
            LIMIT 1
        """, (
            current,
        )).fetchone()

        if row:
            connection.execute("""
                UPDATE mirror_jobs
                SET
                    status = 'running',
                    attempts = attempts + 1,
                    lease_until = ?,
                    updated_at = ?
                WHERE id = ?
            """, (
                current + 180,
                now(),
                row["id"],
            ))

        connection.commit()

    if not row:
        return None

    job = dict(row)
    job["attempts"] += 1

    try:
        job["payload"] = json.loads(
            job.get("payload") or "{}"
        )
    except json.JSONDecodeError:
        job["payload"] = {}

    if not job["payload"]:
        job["payload"] = {
            "codigo": job["codigo"],
            "nombre": job["nombre"],
        }

    return job


def sync_clock_user(
    device: dict,
    codigo: str,
    desired: dict,
    connector=None,
) -> dict:
    profile = normalize_profile(
        codigo,
        desired,
    )

    if connector is None:
        from app.services.zk_devices import _connect

        connector = _connect

    connection = None
    disabled = False

    try:
        connection = connector(
            device,
            timeout=12,
        )

        connection.disable_device()
        disabled = True

        matches = [
            user
            for user in (
                connection.get_users() or []
            )
            if str(user.user_id)
            == str(codigo)
        ]

        if len(matches) > 1:
            raise ClockConflict(
                "El código está duplicado en el reloj; "
                "requiere revisión manual"
            )

        existing = (
            matches[0]
            if matches
            else None
        )

        current = {
            "name": str(
                getattr(
                    existing,
                    "name",
                    codigo,
                )
                or codigo
            ),
            "privilege": int(
                getattr(
                    existing,
                    "privilege",
                    0,
                )
                or 0
            ),
            "password": str(
                getattr(
                    existing,
                    "password",
                    "",
                )
                or ""
            ),
            "group_id": str(
                getattr(
                    existing,
                    "group_id",
                    "1",
                )
                or "1"
            ),
            "card": int(
                getattr(
                    existing,
                    "card",
                    0,
                )
                or 0
            ),
        }

        sent = {
            "name": profile.get(
                "nombre",
                current["name"],
            ),
            "privilege": profile.get(
                "privilege",
                current["privilege"],
            ),
            "password": profile.get(
                "password",
                current["password"],
            ),
            "group_id": profile.get(
                "group_id",
                current["group_id"],
            ),
            "card": profile.get(
                "card",
                current["card"],
            ),
        }

        arguments = {
            **sent,
            "user_id": str(codigo),
        }

        if existing is not None:
            uid = int(
                getattr(
                    existing,
                    "uid",
                    0,
                )
                or 0
            )

            if uid <= 0:
                raise ClockConflict(
                    "El UID físico existente es inválido"
                )

            arguments["uid"] = uid

        connection.set_user(
            **arguments
        )

        verified = [
            user
            for user in (
                connection.get_users() or []
            )
            if str(user.user_id)
            == str(codigo)
        ]

        if len(verified) != 1:
            raise RuntimeError(
                "No se pudo verificar el usuario "
                "después de escribir"
            )

        actual = verified[0]

        checks = {
            "name": str(
                getattr(
                    actual,
                    "name",
                    "",
                )
                or ""
            ).strip(),
            "privilege": int(
                getattr(
                    actual,
                    "privilege",
                    0,
                )
                or 0
            ),
            "password": str(
                getattr(
                    actual,
                    "password",
                    "",
                )
                or ""
            ),
            "group_id": str(
                getattr(
                    actual,
                    "group_id",
                    "",
                )
                or ""
            ),
            "card": int(
                getattr(
                    actual,
                    "card",
                    0,
                )
                or 0
            ),
        }

        mismatches = [
            field
            for field, value in sent.items()
            if checks[field] != value
        ]

        if mismatches:
            raise ClockConflict(
                "El reloj no confirmó estos campos: "
                + ", ".join(mismatches)
            )

        return {
            "confirmed": True,
            "created": existing is None,
            "codigo": str(codigo),
            "uid": int(
                getattr(
                    actual,
                    "uid",
                    0,
                )
                or 0
            ),
            "fields": [
                key
                for key in profile
                if key != "codigo"
            ],
        }

    finally:
        if connection is not None:
            try:
                if disabled:
                    connection.enable_device()
            finally:
                connection.disconnect()


def rename_clock(
    device: dict,
    codigo: str,
    nombre: str,
    connector=None,
) -> dict:
    return sync_clock_user(
        device,
        codigo,
        {
            "nombre": nombre,
        },
        connector,
    )


def process_one() -> bool:
    job = claim()

    if not job:
        return False

    status = "confirmed"
    detail = (
        "Cambios confirmados mediante "
        "lectura del reloj"
    )
    result: dict = {}

    try:
        from app.services.zk_devices import find_device

        device = find_device(
            job["device"]
        )

        if (
            not device
            or not device.get("ip")
        ):
            raise ClockConflict(
                "Reloj no registrado o sin IP"
            )

        result = sync_clock_user(
            device,
            job["codigo"],
            job["payload"],
        )

    except (
        ClockConflict,
        ValueError,
    ) as error:
        status = "blocked"
        detail = str(error)

    except Exception as error:
        status = (
            "failed"
            if job["attempts"]
            >= MAX_ATTEMPTS
            else "retry"
        )

        detail = (
            f"{type(error).__name__}: "
            "no se confirmó la comunicación "
            "o escritura"
        )

    delay = min(
        900,
        15 * (
            2 ** min(
                job["attempts"],
                MAX_ATTEMPTS,
            )
        ),
    )

    with database() as connection:
        connection.execute("""
            UPDATE mirror_jobs
            SET
                status = ?,
                detail = ?,
                result = ?,
                lease_until = 0,
                next_at = ?,
                updated_at = ?
            WHERE id = ?
              AND status = 'running'
        """, (
            status,
            detail,
            json.dumps(
                result,
                ensure_ascii=False,
            ),
            time.time() + delay,
            now(),
            job["id"],
        ))

        connection.commit()

    return True