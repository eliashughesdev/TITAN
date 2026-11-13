import json
from app.services.json_store import DATA_DIR
from pathlib import Path

from fastapi import APIRouter, Depends
from pydantic import BaseModel

from app.core.deps import get_current_user, require_permission

router = APIRouter(
    prefix="/settings",
    tags=["settings"],
    dependencies=[Depends(get_current_user)],
)

# Ajustes del panel (formato propio, distinto de services/app_settings.py).
# Antes se guardaban en integrations/ponches/data/app_settings.json; si el
# archivo nuevo aun no existe, se toma ese como valor inicial.
FILE = DATA_DIR / "panel_settings.json"
LEGACY_FILE = (
    Path(__file__).resolve().parents[3]
    / "data"
    / "app_settings.json"
)

if not FILE.exists() and LEGACY_FILE.exists():
    try:
        FILE.parent.mkdir(parents=True, exist_ok=True)
        FILE.write_text(LEGACY_FILE.read_text(encoding="utf-8"), encoding="utf-8")
    except OSError:
        pass

DEFAULTS = {
    "company_name": "César Iglesias",
    "prod_host": "172.21.20.14",
    "night_start": "22:00",
    "jornada_hours": 8,
    "extra_factor": 0.45,
    "night_factor": 0.15,
    "sunday_factor": 1.0,
    "saturday_premium_factor": 0.0,
    "holiday_factor": 1.0,
    "saturday_base_hours": 0,
    "log_sync_on_filter": False,
    "remote_punch_enabled": False,
}


def _read() -> dict:
    if not FILE.exists():
        return dict(DEFAULTS)

    try:
        data = json.loads(FILE.read_text(encoding="utf-8"))

        if isinstance(data, dict):
            return {**DEFAULTS, **data}
    except Exception:
        pass

    return dict(DEFAULTS)


def _write(data: dict) -> dict:
    FILE.parent.mkdir(parents=True, exist_ok=True)
    merged = {**_read(), **data}

    FILE.write_text(
        json.dumps(
            merged,
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )

    return merged


class SettingsIn(BaseModel):
    company_name: str | None = None
    prod_host: str | None = None
    night_start: str | None = None
    jornada_hours: float | None = None
    extra_factor: float | None = None
    night_factor: float | None = None
    sunday_factor: float | None = None
    saturday_premium_factor: float | None = None
    holiday_factor: float | None = None
    saturday_base_hours: float | None = None
    log_sync_on_filter: bool | None = None
    remote_punch_enabled: bool | None = None

    model_config = {"extra": "allow"}


@router.get("")
def get_settings(
    _user: dict = Depends(
        require_permission("settings.read")
    ),
):
    return _read()


@router.get("/")
def get_settings_slash(
    _user: dict = Depends(
        require_permission("settings.read")
    ),
):
    return _read()


@router.api_route("", methods=["POST", "PUT"])
@router.api_route("/", methods=["POST", "PUT"])
def save_settings(
    body: SettingsIn,
    _user: dict = Depends(
        require_permission("settings.write")
    ),
):
    payload = {
        key: value
        for key, value in body.model_dump().items()
        if value is not None
    }

    return {
        "ok": True,
        "settings": _write(payload),
    }