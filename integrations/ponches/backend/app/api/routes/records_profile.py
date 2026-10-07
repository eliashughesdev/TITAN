from fastapi import (
    APIRouter,
    Depends,
    HTTPException,
)

from app.api.routes.records_clocks import (
    ClockPushIn,
)
from app.api.routes.records_collab import (
    CollabFull,
)
from app.core.deps import require_permission
from app.core.safety import (
    assert_live,
    preview,
)
from app.services import clock_mirror
from app.services.collaborators import (
    get_collab,
)
from app.services.profile_mirror import (
    save_profile,
)
from app.services.zk_devices import (
    find_device,
)

router = APIRouter()


class MirrorProfile(CollabFull):
    nombre_reloj: str = ""


@router.post(
    "/collaborator-profile"
)
def save(
    body: MirrorProfile,
    user: dict = Depends(
        require_permission(
            "collaborators.write"
        )
    ),
):
    return save_profile(
        body.model_dump(),
        user,
    )


@router.post(
    "/collab-push"
)
def synchronize(
    body: ClockPushIn,
    user: dict = Depends(
        require_permission(
            "collaborators.sync",
            "zk.push",
        )
    ),
):
    if user.get("source") != "titan":
        raise HTTPException(
            403,
            "La sincronización requiere "
            "una sesión TitanMDM",
        )

    codigo = body.codigo.strip()

    profile = get_collab(codigo)

    if not profile:
        raise HTTPException(
            404,
            "Guarda la ficha primero",
        )

    names = list(
        dict.fromkeys(
            name.strip()
            for name in body.dispositivos
            if name.strip()
        )
    )

    if (
        not names
        or len(names) > 20
    ):
        raise HTTPException(
            400,
            "Selecciona entre uno "
            "y veinte relojes",
        )

    desired = {
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

    try:
        normalized = (
            clock_mirror.normalize_profile(
                codigo,
                desired,
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

    for name in names:
        if not find_device(name):
            raise HTTPException(
                400,
                f"Reloj no registrado: {name}",
            )

    assert_live(
        body.dry_run,
        body.confirm,
        "sincronizar ficha",
    )

    if body.dry_run:
        return preview(
            "sincronizar ficha",
            codigo=codigo,
            targets=names,
            fields=[
                key
                for key in normalized
                if key != "codigo"
            ],
            preserves=(
                "UID y huellas existentes"
            ),
        )

    items = [
        clock_mirror.enqueue(
            user["organization_id"],
            user["username"],
            name,
            codigo,
            str(
                normalized.get("nombre")
                or ""
            ),
            normalized,
        )
        for name in names
    ]

    return {
        "accepted": True,
        "confirmed": False,
        "mode": "pending",
        "scope": "profile",
        "message": (
            "Envío pendiente; consulta "
            "Escritura en relojes para comprobar "
            "el resultado físico"
        ),
        "results": [
            {
                "device": item["device"],
                "operation_id": item["id"],
                "ok": False,
                "accepted": True,
                "confirmed": False,
                "mode": (
                    "Pendiente de confirmación "
                    "en reloj"
                ),
            }
            for item in items
        ],
    }