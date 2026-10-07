from __future__ import annotations

import argparse
import importlib.metadata
import json
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
BACKEND = ROOT / "integrations" / "ponches" / "backend"
INVENTORY = BACKEND / "data" / "devices_inventory.json"

sys.path.insert(0, str(BACKEND))


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def sdk_version() -> str:
    try:
        return importlib.metadata.version("pyzk")
    except importlib.metadata.PackageNotFoundError:
        return "No instalado"


def load_inventory() -> list[dict[str, Any]]:
    if not INVENTORY.is_file():
        raise RuntimeError(
            f"No se encontró el inventario: {INVENTORY}"
        )

    try:
        content = json.loads(
            INVENTORY.read_text(encoding="utf-8-sig")
        )
    except (OSError, json.JSONDecodeError) as error:
        raise RuntimeError(
            "No se pudo leer el inventario de relojes"
        ) from error

    if not isinstance(content, list):
        raise RuntimeError(
            "El inventario debe contener una lista de relojes"
        )

    return [
        item
        for item in content
        if isinstance(item, dict)
        and str(item.get("name") or "").strip()
    ]


def public_error(error: Exception) -> str:
    return (
        f"{type(error).__name__}: "
        "la consulta no pudo completarse"
    )


def read_method(
    connection: Any,
    method_name: str,
) -> dict[str, Any]:
    method = getattr(
        connection,
        method_name,
        None,
    )

    if not callable(method):
        return {
            "status": "sdk_method_unavailable",
            "value": None,
        }

    try:
        value = method()

        if isinstance(value, bytes):
            value = value.decode(
                "utf-8",
                errors="replace",
            )

        return {
            "status": "read",
            "value": str(value) if value is not None else None,
        }

    except Exception as error:
        return {
            "status": "query_failed",
            "value": None,
            "detail": public_error(error),
        }


def inspect_device(
    device: dict[str, Any],
    timeout: int,
) -> dict[str, Any]:
    name = str(
        device.get("name") or ""
    ).strip()

    ip = str(
        device.get("ip") or ""
    ).strip()

    result: dict[str, Any] = {
        "name": name,
        "ip": ip,
        "configured_model": str(
            device.get("model") or ""
        ),
        "checked_at": utc_now(),
        "status": "not_checked",
        "metadata": {},
        "sdk_methods": {},
        "attendance_write": {
            "status": "not_verified",
            "detail": (
                "Este diagnóstico no realiza escrituras. "
                "La presencia de un método en el SDK "
                "no confirma compatibilidad física."
            ),
        },
    }

    if not ip:
        result["status"] = "missing_ip"
        result["detail"] = (
            "El reloj está registrado sin dirección IP"
        )
        return result

    connection = None
    started = time.monotonic()

    try:
        from app.services.zk_devices import _connect

        connection = _connect(
            device,
            timeout=timeout,
        )

        result["status"] = "connected"

        metadata_methods = {
            "device_name": "get_device_name",
            "firmware": "get_firmware_version",
            "platform": "get_platform",
            "serial": "get_serialnumber",
            "mac": "get_mac",
            "fingerprint_algorithm": "get_fp_version",
            "face_algorithm": "get_face_version",
        }

        for label, method_name in metadata_methods.items():
            result["metadata"][label] = read_method(
                connection,
                method_name,
            )

        # Se inspecciona la existencia de métodos.
        # Ninguno se ejecuta en este bloque.
        inspected_methods = (
            "get_users",
            "set_user",
            "get_templates",
            "save_user_template",
            "get_attendance",
            "set_attendance",
            "write_attendance",
            "add_attendance",
        )

        result["sdk_methods"] = {
            method_name: callable(
                getattr(
                    connection,
                    method_name,
                    None,
                )
            )
            for method_name in inspected_methods
        }

        candidate_writers = [
            method_name
            for method_name in (
                "set_attendance",
                "write_attendance",
                "add_attendance",
            )
            if result["sdk_methods"].get(method_name)
        ]

        result["attendance_write"]["candidate_methods"] = (
            candidate_writers
        )

        if not candidate_writers:
            result["attendance_write"]["detail"] = (
                "No se encontraron los métodos de escritura "
                "de asistencia inspeccionados en el SDK "
                "instalado. Se necesita verificar una vía "
                "documentada del fabricante."
            )

    except Exception as error:
        result["status"] = "connection_failed"
        result["detail"] = public_error(error)

    finally:
        if connection is not None:
            try:
                connection.disconnect()
            except Exception:
                result["disconnect_warning"] = True

        result["elapsed_seconds"] = round(
            time.monotonic() - started,
            2,
        )

    return result


def metadata_value(
    result: dict[str, Any],
    field: str,
) -> str:
    entry = result.get(
        "metadata",
        {},
    ).get(
        field,
        {},
    )

    return str(
        entry.get("value") or "No disponible"
    )


def save_report(
    destination: Path,
    report: dict[str, Any],
) -> None:
    destination.parent.mkdir(
        parents=True,
        exist_ok=True,
    )

    temporary = destination.with_suffix(
        destination.suffix + ".tmp"
    )

    temporary.write_text(
        json.dumps(
            report,
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )

    temporary.replace(destination)


def main() -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Diagnóstico de solo lectura "
            "de relojes registrados en TitanMDM"
        )
    )

    parser.add_argument(
        "--device",
        action="append",
        default=[],
        help=(
            "Nombre exacto del reloj. "
            "Puede repetirse. Sin este argumento "
            "se consultan todos los registrados."
        ),
    )

    parser.add_argument(
        "--timeout",
        type=int,
        default=8,
        choices=range(3, 31),
        metavar="3-30",
    )

    parser.add_argument(
        "--output",
        type=Path,
        default=(
            BACKEND
            / "data"
            / "clock_diagnostics.json"
        ),
    )

    args = parser.parse_args()

    try:
        devices = load_inventory()
    except RuntimeError as error:
        print(str(error))
        return 1

    requested = {
        name.strip()
        for name in args.device
        if name.strip()
    }

    if requested:
        available = {
            str(device["name"]).strip()
            for device in devices
        }

        missing = requested - available

        if missing:
            print(
                "No encontrados en el inventario: "
                + ", ".join(sorted(missing))
            )
            return 1

        devices = [
            device
            for device in devices
            if str(device["name"]).strip() in requested
        ]

    if not devices:
        print(
            "No hay relojes registrados para diagnosticar."
        )
        return 1

    version = sdk_version()

    if version == "No instalado":
        print(
            "pyzk no está instalado en este Python. "
            "Ejecuta el comando usando el entorno "
            "virtual del backend de Ponches."
        )
        return 1

    report: dict[str, Any] = {
        "started_at": utc_now(),
        "sdk": {
            "name": "pyzk",
            "version": version,
        },
        "read_only": True,
        "total": len(devices),
        "items": [],
    }

    print(f"SDK pyzk: {version}")
    print(f"Relojes seleccionados: {len(devices)}")
    print(
        "Consulta secuencial; no se modificarán "
        "usuarios ni asistencia."
    )

    try:
        for index, device in enumerate(
            devices,
            start=1,
        ):
            print(
                f"\n[{index}/{len(devices)}] "
                f"{device['name']}",
                flush=True,
            )

            result = inspect_device(
                device,
                args.timeout,
            )

            report["items"].append(result)

            print(
                f"Estado: {result['status']}",
                flush=True,
            )

            if result["status"] == "connected":
                print(
                    "Modelo leído: "
                    + metadata_value(
                        result,
                        "device_name",
                    )
                )

                print(
                    "Firmware: "
                    + metadata_value(
                        result,
                        "firmware",
                    )
                )

                print(
                    "Plataforma: "
                    + metadata_value(
                        result,
                        "platform",
                    )
                )

            print(
                "Escritura de asistencia: "
                "NO VERIFICADA",
                flush=True,
            )

            # Guarda progreso después de cada reloj.
            save_report(
                args.output,
                report,
            )

    except KeyboardInterrupt:
        report["interrupted"] = True
        print(
            "\nDiagnóstico detenido. "
            "Se conservarán los resultados completados."
        )

    report["finished_at"] = utc_now()
    report["completed"] = len(
        report["items"]
    )

    report["connected"] = sum(
        item["status"] == "connected"
        for item in report["items"]
    )

    save_report(
        args.output,
        report,
    )

    print(
        f"\nConsultados: {report['completed']}"
    )
    print(
        f"Conectados: {report['connected']}"
    )
    print(
        f"Informe: {args.output.resolve()}"
    )

    return (
        0
        if report["connected"] > 0
        else 1
    )


if __name__ == "__main__":
    raise SystemExit(main())