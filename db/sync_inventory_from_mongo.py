"""Sincronizacion INCREMENTAL e idempotente cisa_itam (MongoDB) -> cisa_ti_itam (SQL Server).

Reemplaza el uso de migrate_inventory_from_mongo.py para completar datos:
aquel script BORRA todas las tablas antes de reinsertar (destruiria lo creado
desde TitanMDM) y descartaba columnas que no existian en SQL.

Este script:
  * nunca borra filas ni toca MongoDB (solo lectura del origen);
  * agrega (ALTER TABLE ADD, nullable) las columnas que la migracion perdio:
    inventario de hardware de activos, employee_id/mac_address de entregas,
    employee_id/serial_number/branch_id/department_id de devoluciones;
  * inserta los documentos cuyo id no existe en SQL;
  * actualiza filas existentes cuando MongoDB es mas reciente
    (updated_at mayor) o la tabla no tiene updated_at (MongoDB es el maestro
    mientras la app anterior siga en uso);
  * rellena before_json/after_json del historial desde before/after;
  * guarda un respaldo JSON de cada fila modificada.

Uso (simulacion por defecto):
    python db/sync_inventory_from_mongo.py
    python db/sync_inventory_from_mongo.py --apply
"""
from __future__ import annotations

import argparse
import json
from datetime import date, datetime, timezone
from decimal import Decimal
from pathlib import Path

import pyodbc
from bson import ObjectId
from pymongo import MongoClient

MONGO_URL = "mongodb://localhost:27017"
MONGO_DB = "cisa_itam"
SQL = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=localhost\\SQLEXPRESS;"
    "DATABASE=cisa_ti_itam;"
    "Trusted_Connection=yes;"
    "TrustServerCertificate=yes;"
)
BACKUP_DIR = Path(__file__).resolve().parent.parent / ".titan-local" / "backups"

# Orden respetando dependencias (padres antes que hijos).
TABLES = [
    "roles", "categories", "branches", "departments", "manufacturers", "suppliers",
    "locations", "asset_models", "employees", "users", "settings", "assets",
    "deliveries", "returns", "receptions", "maintenance", "inventory_sessions",
    "inventory_counts", "asset_history", "audit_logs", "notifications",
]

# Columnas perdidas en la migracion original (todas nullable).
MISSING_COLUMNS = {
    "assets": {
        "manufacturer": "NVARCHAR(200)", "model": "NVARCHAR(200)", "os_name": "NVARCHAR(200)",
        "processor": "NVARCHAR(200)", "ram_gb": "FLOAT", "disk_total_gb": "FLOAT",
        "disk_free_gb": "FLOAT", "last_user": "NVARCHAR(200)", "last_seen": "NVARCHAR(40)",
        "last_inventory_at": "NVARCHAR(40)", "source": "NVARCHAR(60)", "deleted_by": "NVARCHAR(160)",
    },
    "deliveries": {"employee_id": "NVARCHAR(36)", "mac_address": "NVARCHAR(40)"},
    "returns": {
        "employee_id": "NVARCHAR(36)", "serial_number": "NVARCHAR(120)",
        "branch_id": "NVARCHAR(36)", "department_id": "NVARCHAR(36)",
    },
}

# Campo Mongo -> columna SQL cuando difieren.
FIELD_ALIASES = {
    "before_json": ["before_json", "before"],
    "after_json": ["after_json", "after"],
    "deleted": ["deleted", "is_deleted"],
}

NUMERIC = {"int", "bigint", "smallint", "tinyint", "decimal", "numeric", "float", "real", "money"}


def as_sql(value, kind):
    if value is None:
        return None
    if kind == "bit":
        if isinstance(value, bool):
            return 1 if value else 0
        if isinstance(value, (int, float)):
            return 1 if value else 0
        return 1 if str(value).strip().lower() in {"1", "true", "yes", "si"} else 0
    if kind in NUMERIC:
        try:
            number = float(value)
        except (TypeError, ValueError):
            return None
        return int(number) if kind in {"int", "bigint", "smallint", "tinyint"} else number
    if isinstance(value, ObjectId):
        return str(value)
    if isinstance(value, (datetime, date)):
        if isinstance(value, datetime) and value.tzinfo is None:
            value = value.replace(tzinfo=timezone.utc)
        return value.isoformat()
    if isinstance(value, (list, dict)):
        return json.dumps(value, ensure_ascii=False, default=str)
    if isinstance(value, Decimal):
        return str(value)
    return str(value)


def normalized(value):
    """Comparacion tolerante (bit/float/texto) para detectar cambios reales."""
    if value is None:
        return None
    if isinstance(value, bool):
        return int(value)
    if isinstance(value, Decimal):
        value = float(value)
    if isinstance(value, float) and value.is_integer():
        return int(value)
    return value if isinstance(value, (int, float)) else str(value)


def present(doc, column):
    """True si el documento trae el campo (aunque sea null)."""
    return any(name in doc for name in FIELD_ALIASES.get(column, [column]))


def pick(doc, column):
    for name in FIELD_ALIASES.get(column, [column]):
        if name in doc and doc[name] is not None:
            return doc[name]
    return None


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--apply", action="store_true", help="Escribe en SQL Server (por defecto solo simula).")
    args = parser.parse_args()

    mongo = MongoClient(MONGO_URL, serverSelectionTimeoutMS=5000)
    mongo.admin.command("ping")
    source = mongo[MONGO_DB]

    sql = pyodbc.connect(SQL, autocommit=False)
    cur = sql.cursor()

    # 1. Columnas perdidas (DDL idempotente).
    ddl = []
    for table, cols in MISSING_COLUMNS.items():
        for column, kind in cols.items():
            cur.execute(
                "SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=? AND COLUMN_NAME=?",
                table, column)
            if cur.fetchone() is None:
                ddl.append(f"ALTER TABLE [{table}] ADD [{column}] {kind} NULL")

    if args.apply:
        for statement in ddl:
            cur.execute(statement)
        sql.commit()

    cur.execute("SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS")
    schema: dict[str, dict[str, str]] = {}
    for table, column, kind in cur.fetchall():
        schema.setdefault(table, {})[column] = kind

    # En simulacion, contar tambien las columnas que se agregarian.
    if not args.apply:
        for table, cols in MISSING_COLUMNS.items():
            for column, kind in cols.items():
                schema.setdefault(table, {}).setdefault(column, kind.split("(")[0].lower())

    collections = set(source.list_collection_names())
    summary = {}
    backup = {}

    for table in TABLES:
        if table not in collections or table not in schema or "id" not in schema[table]:
            continue

        columns = schema[table]
        cur.execute(f"SELECT * FROM [{table}]")
        sql_cols = [c[0] for c in cur.description]
        existing = {str(row[0]): dict(zip(sql_cols, row)) for row in cur.fetchall()}

        inserts, updates = [], []

        for doc in source[table].find({}):
            doc_id = doc.get("id")
            if not doc_id:
                continue

            record = {c: as_sql(pick(doc, c), k) for c, k in columns.items()}
            record["id"] = str(doc_id)
            current = existing.get(str(doc_id))

            if current is None:
                inserts.append(record)
                continue

            mongo_newer = (
                "updated_at" not in columns
                or not current.get("updated_at")
                or str(record.get("updated_at") or "") >= str(current.get("updated_at") or "")
            )

            changes = {
                c: v for c, v in record.items()
                if c != "id"
                and c in current
                # null explicito (p.ej. activo desasignado) tambien se propaga.
                and (v is not None or (mongo_newer and present(doc, c)))
                and normalized(v) != normalized(current[c])
                # Las columnas recuperadas se rellenan aunque SQL sea mas reciente.
                and (mongo_newer or current[c] is None)
            }

            if changes:
                updates.append((str(doc_id), changes))
                backup.setdefault(table, []).append({"id": str(doc_id), "antes": {c: current[c] for c in changes}})

        if args.apply:
            for record in inserts:
                names = ",".join(f"[{c}]" for c in record)
                marks = ",".join("?" for _ in record)
                cur.execute(f"INSERT INTO [{table}] ({names}) VALUES ({marks})", *record.values())
            for doc_id, changes in updates:
                assignments = ",".join(f"[{c}]=?" for c in changes)
                cur.execute(f"UPDATE [{table}] SET {assignments} WHERE id=?", *changes.values(), doc_id)

        summary[table] = {
            "insertados" if args.apply else "a_insertar": len(inserts),
            "actualizados" if args.apply else "a_actualizar": len(updates),
            "columnas_actualizadas": sorted({c for _, ch in updates for c in ch}),
        }

    if args.apply:
        BACKUP_DIR.mkdir(parents=True, exist_ok=True)
        stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        backup_file = BACKUP_DIR / f"itam-sync-{stamp}.json"
        backup_file.write_text(json.dumps(backup, ensure_ascii=False, indent=1, default=str), encoding="utf-8")
        sql.commit()
        print("RESPALDO", backup_file)
    else:
        sql.rollback()

    print("MODO", "APLICADO" if args.apply else "SIMULACION (use --apply para escribir)")
    print("DDL", json.dumps(ddl, ensure_ascii=False, indent=1))
    print("RESUMEN", json.dumps(summary, ensure_ascii=False, indent=1))
    mongo.close()


if __name__ == "__main__":
    main()
