from __future__ import annotations

import hmac
from uuid import UUID

from fastapi import Depends, Header, HTTPException
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.core.config import settings
from app.core.permissions import (
    ADMIN_ROLES,
    ALL_OPERATIONS,
    has_permission,
    normalize_role,
)
from app.core.security import decode_access_token
from app.services.app_users import find_user, public_user

_bearer = HTTPBearer(auto_error=False)


def get_current_user(
    creds: HTTPAuthorizationCredentials | None = Depends(_bearer),
    x_actor: str | None = Header(None, alias="X-Actor"),
    x_titan_integration_key: str | None = Header(
        None, alias="X-Titan-Integration-Key"
    ),
    x_titan_actor: str | None = Header(
        None, alias="X-Titan-Actor"
    ),
    x_titan_organization: str | None = Header(
        None, alias="X-Titan-Organization"
    ),
    x_titan_access: str | None = Header(
        None, alias="X-Titan-Access"
    ),
    x_titan_operations: str | None = Header(
        None, alias="X-Titan-Operations"
    ),
) -> dict:
    if x_titan_integration_key is not None:
        expected = settings.TITAN_PONCHES_INTEGRATION_KEY.strip()

        if len(expected) < 32 or not hmac.compare_digest(
            expected,
            x_titan_integration_key,
        ):
            raise HTTPException(
                401,
                "Integración no autorizada",
            )

        try:
            actor = str(UUID(x_titan_actor or ""))
            organization = str(UUID(x_titan_organization or ""))
        except (ValueError, TypeError):
            raise HTTPException(
                401,
                "Identidad de TitanMDM inválida",
            )

        if x_titan_access not in {"manage", "delegate"}:
            raise HTTPException(
                403,
                "Delegación de permisos requerida",
            )

        operations = {
            value.strip()
            for value in (x_titan_operations or "").split(",")
            if value.strip()
        }

        if not operations.issubset(set(ALL_OPERATIONS)):
            raise HTTPException(
                403,
                "Operación de integración desconocida",
            )

        # Las identidades de Titan reciben exclusivamente
        # las operaciones autorizadas por el backend .NET.
        return {
            "username": actor,
            "name": actor,
            "organization_id": organization,
            "source": "titan",
            "role": (
                "admin"
                if x_titan_access == "manage"
                else "delegate"
            ),
            "operations": sorted(operations),
            "permissions": {
                "operations": sorted(operations),
            },
            "active": True,
        }

    token = creds.credentials if creds else None

    if not token:
        raise HTTPException(401, "Token requerido")

    payload = decode_access_token(token)

    if not payload or not payload.get("sub"):
        raise HTTPException(
            401,
            "Token inválido o expirado",
        )

    user = find_user(str(payload["sub"]))

    if not user or not user.get("active", True):
        raise HTTPException(
            401,
            "Usuario inactivo",
        )

    result = public_user(user)

    if x_actor:
        result["actor_header"] = x_actor

    return result


def require_role(*roles: str):
    allowed = {role.lower() for role in roles}

    def check(
        user: dict = Depends(get_current_user),
    ) -> dict:
        role = normalize_role(user.get("role"))

        if role in ADMIN_ROLES or role in allowed:
            return user

        raise HTTPException(403, "Sin permiso")

    return check


def require_admin(
    user: dict = Depends(get_current_user),
) -> dict:
    if normalize_role(user.get("role")) not in ADMIN_ROLES:
        raise HTTPException(
            403,
            "Requiere rol administrativo",
        )

    return user


def require_permission(*operations: str):
    needed = set(operations)

    def check(
        user: dict = Depends(get_current_user),
    ) -> dict:
        if user.get("source") == "titan":
            permitted = (
                bool(needed)
                and needed.issubset(
                    set(user.get("operations", []))
                )
            )
        else:
            permitted = has_permission(user, *operations)

        if permitted:
            return user

        raise HTTPException(
            403,
            "Sin permiso para: " + ", ".join(sorted(needed)),
        )

    return check