# TitanMDM Break-Glass Procedure

## Objetivo

TitanMDM mantiene una cuenta administrativa local de emergencia para recuperar
acceso cuando Entra ID, OIDC o una dependencia externa de identidad no está
disponible.

## Cuenta

La cuenta bootstrap:

`superadmin@titanmdm.local`

es la cuenta break-glass inicial.

No debe utilizarse como cuenta administrativa cotidiana.

## Creación

La contraseña inicial se recibe exclusivamente mediante:

`TITAN_BOOTSTRAP_PASSWORD`

durante el bootstrap de una instalación nueva.

La contraseña nunca debe almacenarse en:

- Git
- appsettings.json
- scripts versionados
- tickets
- documentación
- correo electrónico

## Custodia

La contraseña debe mantenerse en el vault corporativo aprobado.

Acceso recomendado:

- mínimo dos custodios autorizados;
- MFA/vault controls cuando el vault lo soporte;
- registro de acceso;
- rotación después de cada utilización.

## Cuándo utilizarla

Solo cuando:

1. Entra ID/OIDC no está disponible;
2. existe una emergencia operativa;
3. se perdió acceso administrativo normal;
4. una modificación RBAC dejó la plataforma inaccesible.

## Procedimiento

1. Registrar incidente.
2. Obtener aprobación del responsable autorizado.
3. Recuperar la credencial desde el vault.
4. Iniciar sesión directamente contra TitanMDM.
5. Ejecutar exclusivamente las acciones necesarias.
6. Verificar auditoría.
7. Rotar contraseña inmediatamente.
8. Revocar sesiones existentes.
9. Cerrar el incidente documentando las acciones realizadas.

## Restricciones

La cuenta break-glass:

- no debe utilizarse para navegación diaria;
- no debe compartirse por chat/correo;
- no debe estar integrada con Entra;
- no debe utilizarse por automatizaciones;
- no debe almacenarse en navegadores;
- debe conservar el rol del sistema `SuperAdmin`.

## Validación periódica

Cada trimestre:

- comprobar que la cuenta existe;
- comprobar que está activa;
- validar acceso en ambiente controlado;
- verificar recuperación de credencial;
- revisar accesos históricos;
- rotar la credencial si la política corporativa lo requiere.

## Incidente

Todo uso de break-glass debe tratarse como evento administrativo sensible y
revisarse posteriormente.