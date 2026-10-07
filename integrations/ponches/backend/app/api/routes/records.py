from fastapi import (
    APIRouter,
    Depends,
)

from app.api.routes.records_appmeta import (
    router as appmeta_router,
)
from app.api.routes.records_clocks import (
    router as clocks_router,
)
from app.api.routes.records_collab import (
    router as collab_router,
)
from app.api.routes.records_export_catalog import (
    router as export_catalog_router,
)
from app.api.routes.records_inventory import (
    router as inventory_router,
)
from app.api.routes.records_mirror import (
    router as mirror_router,
)
from app.api.routes.records_profile import (
    router as profile_router,
)
from app.api.routes.records_query import (
    router as query_router,
)
from app.api.routes.records_remote import (
    router as remote_router,
)
from app.api.routes.records_schedules import (
    router as schedules_router,
)
from app.api.routes.records_sync import (
    router as sync_router,
)
from app.core.deps import (
    get_current_user,
)

# Se eliminan las versiones anteriores de estas dos rutas.
# profile_router contiene las implementaciones persistentes,
# verificadas y protegidas por RBAC.
collab_router.routes = [
    route
    for route in collab_router.routes
    if getattr(
        route,
        "path",
        "",
    )
    != "/collaborator-profile"
]

clocks_router.routes = [
    route
    for route in clocks_router.routes
    if getattr(
        route,
        "path",
        "",
    )
    != "/collab-push"
]

router = APIRouter(
    prefix="/records",
    tags=["records"],
    dependencies=[
        Depends(
            get_current_user
        )
    ],
)

router.include_router(query_router)
router.include_router(sync_router)
router.include_router(schedules_router)
router.include_router(remote_router)
router.include_router(appmeta_router)
router.include_router(inventory_router)
router.include_router(collab_router)
router.include_router(clocks_router)
router.include_router(mirror_router)
router.include_router(export_catalog_router)
router.include_router(profile_router)