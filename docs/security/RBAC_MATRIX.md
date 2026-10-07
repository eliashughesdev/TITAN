# TitanMDM RBAC Matrix

## Objetivo

TitanMDM utiliza autorización server-side basada en:

Permission + Organization + Scope

Los roles agrupan permisos, pero los endpoints autorizan mediante permisos
atómicos.

La UI nunca constituye un control de seguridad.

---

## Scopes

TitanMDM soporta:

| Scope | Uso |
|---|---|
| Organization | Acceso completo dentro de una organización |
| Site | Acceso limitado a una localidad |
| Department | Acceso limitado a un departamento |
| Group | Acceso limitado a un grupo de dispositivos |

Organization domina scopes inferiores únicamente dentro de la misma
organización.

No existe inferencia automática entre Site, Department y Group.

---

## Roles recomendados

### SuperAdmin

Administración completa de TitanMDM.

Uso:

- configuración inicial;
- administración global;
- break-glass.

No recomendado para operación cotidiana.

### Platform Administrator

Administración de endpoints y configuración operacional.

### Windows Administrator

Workspace Windows y administración de dispositivos Windows.

### Android Administrator

Workspace Android y Android Enterprise.

### Security Operator

Security Center, compliance y acciones autorizadas.

### Remote Support Operator

Consulta de dispositivos y soporte remoto.

### Helpdesk Administrator

Administración de Helpdesk, routing, zonas y configuración.

### Helpdesk Agent

Tickets y soporte conforme al scope asignado.

### Auditor

Acceso read-only a auditoría y reportes.

### Ponches Administrator

Administración del módulo Ponches.

---

## Matriz principal

| Módulo | Read | Manage / Execute |
|---|---|---|
| Dashboard | dashboard.view | dashboard.global.view |
| Windows Workspace | workspace.windows.view | según permiso funcional |
| Android Workspace | workspace.android.view | según permiso funcional |
| Administration | workspace.administration.view | settings.manage |
| Devices | devices.view | devices.create / devices.update / devices.delete / devices.commands |
| Enrollment | enrollment.view | enrollment.manage |
| Policies | policies.view | policies.manage |
| Applications | apps.view | apps.manage |
| Compliance | compliance.view | compliance.manage |
| Security | security.view | security.manage |
| Remote Support | remote.view | remote.manage |
| Reports | reports.view | reports.export |
| Users | users.view | users.manage |
| Roles | roles.view | roles.manage |
| Audit | audit.view | read-only |
| Settings | settings.view | settings.manage |
| Helpdesk | helpdesk.view / tickets.view | helpdesk.manage |
| Tickets | tickets.view | tickets.create / tickets.assign / tickets.comment / tickets.close |
| Geofencing | geofencing.view | geofencing.manage |
| Kiosk | kiosk.view | kiosk.manage |

---

## Reglas críticas

### Usuario

Todo usuario debe:

1. pertenecer a una Organization;
2. poseer al menos un Role activo;
3. poseer únicamente los permisos derivados de roles activos;
4. respetar los scopes asignados.

### Organización

Nunca se permite acceder a recursos cuyo OrganizationId sea diferente al
`organization_id` del token.

### SecurityVersion

Cada Access Token contiene:

`security_version`

Cuando ocurre una modificación sensible:

- cambio de contraseña;
- desactivación;
- cambio de rol;
- cambio de permisos;
- cambio de scope;

TitanMDM incrementa SecurityVersion y revoca RefreshTokens.

Un Access Token emitido con una versión anterior deja de ser válido.

### SuperAdmin

SuperAdmin no recibe bypass hardcoded.

El rol recibe los permisos activos mediante el seed.

Esto mantiene la autorización auditable y uniforme.

---

## Entra ID

Entra autentica la identidad.

TitanMDM autoriza la identidad.

Un usuario autenticado correctamente por Microsoft no puede utilizar TitanMDM
si:

- no existe vínculo explícito;
- el usuario TitanMDM está desactivado;
- no tiene rol activo;
- sus permisos no permiten la acción;
- su scope no permite el recurso.

---

## Break-glass

`superadmin@titanmdm.local` se reserva como cuenta break-glass inicial.

Consultar:

`docs/security/BREAK_GLASS.md`

---

## Prohibiciones

No se permite:

- confiar solamente en ocultar botones del frontend;
- autorizar por correo enviado por el cliente;
- aceptar OrganizationId proporcionado por el frontend;
- hacer bypass de permisos por nombre de usuario;
- hacer bypass general por rol SuperAdmin;
- inferir permisos Site/Department/Group sin relación explícita;
- reutilizar Access Tokens después de revocación de SecurityVersion.

---

## Definition of Done RBAC

La Fase de RBAC solo se considera terminada cuando:

- autorización server-side está activa;
- permisos atómicos están definidos;
- roles no permiten escalación lateral;
- Organization isolation está aplicada;
- Scope evaluator está probado;
- cambios RBAC generan auditoría;
- sesiones se invalidan tras cambios sensibles;
- lockout y rate limiting están activos;
- Entra no sustituye autorización interna;
- break-glass está documentado;
- tests críticos pasan.