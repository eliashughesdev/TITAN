"""Copia cisa_itam (Mongo) hacia cisa_ti_itam (SQL Server).  *** DESTRUCTIVO ***

ADVERTENCIA: este script ejecuta DELETE FROM sobre TODAS las tablas ITAM antes
de reinsertar. Borra cualquier dato creado desde TitanMDM que no exista en
MongoDB. Solo sirve para una carga inicial en una base vacia.

Para completar o actualizar datos use db/sync_inventory_from_mongo.py
(incremental, sin borrados, con simulacion y respaldo).

Requiere TITAN_ALLOW_DESTRUCTIVE_MIGRATION=1 para ejecutarse.
"""
from __future__ import annotations

import json
from datetime import datetime, date
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
SNAPSHOT = Path(r"C:\TitanMDM\src\frontend\titanmdm-web\src\pages\inventory\inventoryData.json")
SKIP_FRONT = {"signature", "photos", "documents", "password_hash", "password", "_id"}

DELETE_ORDER = [
    "inventory_counts", "returns", "deliveries", "receptions", "asset_history",
    "assets", "inventory_sessions", "locations", "asset_models", "employees",
    "users", "audit_logs", "notifications", "maintenance", "tickets",
    "warranties", "licenses", "invoices", "purchase_orders", "software",
    "settings", "login_attempts", "suppliers", "manufacturers", "departments",
    "branches", "categories", "roles",
]
INSERT_ORDER = [
    "roles", "categories", "branches", "departments", "manufacturers", "suppliers",
    "locations", "asset_models", "employees", "users", "settings", "assets",
    "deliveries", "returns", "receptions", "maintenance", "software", "licenses",
    "warranties", "purchase_orders", "invoices", "inventory_sessions",
    "inventory_counts", "asset_history", "audit_logs", "notifications",
    "tickets", "login_attempts",
]
ALIASES = {
    "timestamp": ["timestamp", "created_at", "at"],
    "user_email": ["user_email", "email"],
    "user_name": ["user_name", "user", "name"],
    "detail": ["detail", "description", "message"],
    "entity_type": ["entity_type", "entity", "collection"],
    "description": ["description", "detail", "message"],
    "read": ["read", "is_read"],
    "permissions": ["permissions"],
}


def as_text(value):
    if value is None:
        return None
    if isinstance(value, ObjectId):
        return str(value)
    if isinstance(value, (datetime, date)):
        return value.isoformat()
    if isinstance(value, bool):
        return value
    if isinstance(value, (int, float, Decimal)):
        return value
    if isinstance(value, (list, dict)):
        return json.dumps(value, ensure_ascii=False, default=str)
    return str(value)


def bit(value):
    if value is None:
        return None
    if isinstance(value, bool):
        return 1 if value else 0
    if isinstance(value, (int, float)):
        return 1 if value else 0
    return 1 if str(value).lower() in {"1", "true", "yes", "si"} else 0


def pick(doc, names):
    for name in names:
        if name in doc and doc[name] is not None:
            return doc[name]
    return None


def main():
    import os

    if os.environ.get("TITAN_ALLOW_DESTRUCTIVE_MIGRATION") != "1":
        raise SystemExit(
            "Bloqueado: este script BORRA todas las tablas ITAM. "
            "Use db/sync_inventory_from_mongo.py para completar datos. "
            "Si realmente necesita una carga inicial destructiva, defina "
            "TITAN_ALLOW_DESTRUCTIVE_MIGRATION=1.")

    mongo = MongoClient(MONGO_URL, serverSelectionTimeoutMS=5000)
    mongo.admin.command("ping")
    source = mongo[MONGO_DB]
    sql = pyodbc.connect(SQL)
    sql.autocommit = False
    cur = sql.cursor()
    cur.execute("SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS")
    columns = {}
    types = {}
    for table, column, kind in cur.fetchall():
        columns.setdefault(table, []).append(column)
        types[(table, column)] = kind

    cur.execute("""
        IF OBJECT_ID('employees','U') IS NULL
        CREATE TABLE employees(
            id NVARCHAR(36) PRIMARY KEY,
            code NVARCHAR(60) NULL,
            name NVARCHAR(160) NULL,
            department_id NVARCHAR(36) NULL,
            supervisor NVARCHAR(160) NULL,
            email NVARCHAR(160) NULL,
            phone NVARCHAR(40) NULL,
            position NVARCHAR(100) NULL,
            branch_id NVARCHAR(36) NULL,
            notes NVARCHAR(MAX) NULL,
            is_active BIT DEFAULT 1,
            deleted BIT DEFAULT 0,
            deleted_at NVARCHAR(40) NULL,
            created_at NVARCHAR(40) NULL,
            updated_at NVARCHAR(40) NULL,
            updated_by NVARCHAR(160) NULL,
            created_by NVARCHAR(160) NULL
        )
    """)
    if "employees" not in columns:
        columns["employees"] = [
            "id", "code", "name", "department_id", "supervisor", "email", "phone",
            "position", "branch_id", "notes", "is_active", "deleted", "deleted_at",
            "created_at", "updated_at", "updated_by", "created_by",
        ]
        for column in columns["employees"]:
            types[("employees", column)] = "bit" if column in {"is_active", "deleted"} else "nvarchar"

    docs = {}
    for name in source.list_collection_names():
        docs[name] = list(source[name].find({}))

    counts = []
    if docs.get("inventory_counts"):
        counts = docs["inventory_counts"]
    else:
        for session in docs.get("inventory_sessions", []):
            for item in session.get("counts") or []:
                row = dict(item)
                row.setdefault("session_id", session.get("id"))
                row.setdefault("id", f"{session.get('id')}-{row.get('asset_tag') or row.get('code')}")
                counts.append(row)
    docs["inventory_counts"] = counts

    ids = {
        name: {str(doc.get("id")) for doc in rows if doc.get("id")}
        for name, rows in docs.items()
    }

    def fk(table, value):
        if value is None:
            return None
        text = str(value)
        return text if text in ids.get(table, set()) else None

    cur.execute("EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'")
    for table in DELETE_ORDER:
        if table in columns:
            cur.execute(f"DELETE FROM [{table}]")

    inserted = {}
    for table in INSERT_ORDER:
        rows = docs.get(table) or []
        if table not in columns or not rows:
            inserted[table] = 0
            continue
        cols = columns[table]
        payload = []
        for doc in rows:
            record = {}
            for column in cols:
                raw = pick(doc, ALIASES.get(column, [column]))
                kind = types.get((table, column), "nvarchar")
                if column.endswith("_id") and column != "id":
                    parent = {
                        "category_id": "categories",
                        "manufacturer_id": "manufacturers",
                        "model_id": "asset_models",
                        "branch_id": "branches",
                        "department_id": "departments",
                        "location_id": "locations",
                        "supplier_id": "suppliers",
                        "asset_id": "assets",
                        "delivery_id": "deliveries",
                        "session_id": "inventory_sessions",
                        "user_id": "users",
                    }.get(column)
                    raw = fk(parent, raw) if parent else as_text(raw)
                if kind == "bit":
                    record[column] = bit(raw)
                elif kind in {"int", "decimal", "numeric", "float", "money"}:
                    try:
                        record[column] = None if raw in (None, "") else float(raw) if kind != "int" else int(float(raw))
                    except (TypeError, ValueError):
                        record[column] = None
                else:
                    record[column] = as_text(raw)
            if table == "assets" and not record.get("asset_tag"):
                record["asset_tag"] = record.get("id") or "SIN-TAG"
            if table == "assets" and not record.get("name"):
                record["name"] = record.get("asset_tag")
            if table == "assets" and not record.get("status"):
                record["status"] = "disponible"
            if not record.get("id") and table != "login_attempts":
                continue
            payload.append(tuple(record[column] for column in cols))
        if not payload:
            inserted[table] = 0
            continue
        marks = ",".join("?" for _ in cols)
        names = ",".join(f"[{column}]" for column in cols)
        cur.fast_executemany = True
        cur.executemany(f"INSERT INTO [{table}] ({names}) VALUES ({marks})", payload)
        inserted[table] = len(payload)

    cur.execute("EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'")
    sql.commit()

    def clean(doc):
        out = {}
        for key, value in doc.items():
            if key in SKIP_FRONT or key.startswith("_"):
                continue
            out[key] = as_text(value) if not isinstance(value, (int, float, bool)) else value
        return out

    snapshot = {
        "assets": [clean(doc) for doc in docs.get("assets", [])],
        "employees": [clean(doc) for doc in docs.get("employees", [])],
        "deliveries": [clean(doc) for doc in docs.get("deliveries", [])],
        "returns": [clean(doc) for doc in docs.get("returns", [])],
        "departments": [clean(doc) for doc in docs.get("departments", [])],
        "branches": [clean(doc) for doc in docs.get("branches", [])],
        "categories": [clean(doc) for doc in docs.get("categories", [])],
        "manufacturers": [clean(doc) for doc in docs.get("manufacturers", [])],
        "models": [clean(doc) for doc in docs.get("asset_models", [])],
        "locations": [clean(doc) for doc in docs.get("locations", [])],
        "suppliers": [clean(doc) for doc in docs.get("suppliers", [])],
        "sessions": [clean(doc) for doc in docs.get("inventory_sessions", [])],
        "counts": [clean(doc) for doc in docs.get("inventory_counts", [])],
        "maintenance": [clean(doc) for doc in docs.get("maintenance", [])],
        "settings": [clean(doc) for doc in docs.get("settings", [])],
        "history": [clean(doc) for doc in docs.get("asset_history", [])],
    }
    SNAPSHOT.write_text(json.dumps(snapshot, ensure_ascii=False), encoding="utf-8")
    print("SQL", json.dumps(inserted, ensure_ascii=False))
    print("SNAPSHOT", SNAPSHOT, "bytes", SNAPSHOT.stat().st_size)
    mongo.close()


if __name__ == "__main__":
    main()
