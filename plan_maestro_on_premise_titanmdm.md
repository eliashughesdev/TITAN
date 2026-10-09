# TitanMDM Enterprise 1.0 Arquitectura y Planificación

## TITANMDM - PLAN MAESTRO DE IMPLEMENTACIÓN ON-PREMISE
### Arquitectura prioritaria de producción con compatibilidad futura Azure / Híbrida

| Documento | Valor |
| :--- | :--- |
| **Producto** | TitanMDM Enterprise 1.0 |
| **Enfoque** | Producción empresarial segura y multi-localidad |
| **Cobertura** | 9 localidades + crecimiento futuro |
| **Fecha** | 01 de octubre de 2026 |

*Objetivo:* convertir el repositorio actual en una plataforma Enterprise operable, segura, instalable, auditable y mantenible, con bases sólidas para despliegue on-premise, híbrido y cloud.

---

## Resumen ejecutivo

Este documento redefine TitanMDM con prioridad on-premise. El objetivo es que la empresa pueda instalar y operar la plataforma dentro de su infraestructura, atender aproximadamente nueve localidades y mantener una ruta técnica limpia hacia un escenario híbrido o Azure. La instalación final no dependerá de procedimientos manuales dispersos: bases de datos, tablas, servicios, dependencias, configuración, agentes, Python/Ponches, seguridad y verificaciones se provisionarán mediante instaladores y automatizaciones versionadas.

El modelo recomendado separa el servidor central de los componentes endpoint. El servidor on-premise aloja frontend, API, SQL Server, workers, almacenamiento y el servicio Python de Ponches cuando la conectividad con BioTime lo permita. Los equipos Windows reciben TitanMDM Agent + RemoteHost. Las localidades pueden utilizar Titan Edge Connector cuando los relojes, BioTime o recursos LAN no sean accesibles de forma centralizada.

---

## 1. Principios rectores

| Principio | Aplicación en TitanMDM |
| :--- | :--- |
| **Security by design** | Identidad, autorización, cifrado, hardening, secretos y auditoría desde el diseño. |
| **Zero Trust** | Ningún agente, usuario, conector o servicio es confiable por ubicación de red. |
| **Automatización** | Instalación, migraciones, despliegues, pruebas y actualizaciones deben ser reproducibles. |
| **Least privilege** | Usuarios, servicios y bases de datos operan con los permisos mínimos necesarios. |
| **Observable by default** | Logs estructurados, métricas, health checks, correlation IDs y alertas en todos los componentes. |
| **Fail safely** | Ante una dependencia caída el sistema degrada funciones de forma controlada, no pierde datos silenciosamente. |
| **Portable architecture** | El diseño primario es on-premise, pero los contratos de configuración, almacenamiento, identidad y servicios deben permitir evolución a Azure/híbrido sin reescritura. |
| **Definition of Done real** | Compilar no equivale a terminado: cada módulo requiere pruebas, seguridad, auditoría, documentación y operabilidad. |

---

## 2. Alcance funcional objetivo

* Unified Endpoint Management para Windows y Android.
* Inventario de hardware, software, servicios, procesos, usuarios, seguridad y cumplimiento.
* Distribución empresarial de aplicaciones y futuras capacidades de patch management.
* Políticas, grupos estáticos/dinámicos, automatizaciones y remediación.
* Soporte remoto con UAC/Secure Desktop, multi-monitor, auditoría y control de concurrencia.
* Helpdesk/ITSM integrado a dispositivos, usuarios, sitios, correo, SLA, KPIs y soporte remoto.
* Módulo de Ponches integrado mediante servicio Python, BioTimeDB y relojes ZKTeco.
* Android Enterprise: enrollment, policies, aplicaciones, kiosk, compliance y acciones remotas.
* Titan Assistant/Fiorella con permisos, confirmaciones y registro completo de acciones.
* Reportes y exportaciones PDF/XLSX/CSV, reportes programados y trazabilidad.
* Gestión multi-localidad para aproximadamente 9 sitios, departamentos y grupos de trabajo.
* Observabilidad, respaldo, recuperación, actualizaciones y ciclo de vida de releases.

---

## 3. Criterios globales de "terminado"

| Dimensión | Requisito mínimo para declarar DONE |
| :--- | :--- |
| **Funcional** | Frontend, backend y flujos reales completados; errores y estados vacíos manejados. |
| **Seguridad** | RBAC, validación, rate limiting cuando aplique, secretos externos, auditoría y revisión de amenazas. |
| **Datos** | Migraciones idempotentes, constraints, índices, seed controlado, backup y restauración probados. |
| **Agentes** | Instalación, enrollment, reconnect, comandos, resultados, self-update, repair y uninstall. |
| **Pruebas** | Unit, integration y E2E para flujos críticos; regresiones incorporadas al pipeline. |
| **UX** | Responsive, consistente, accesible, sin JSON crudo ni acciones ambiguas. |
| **Operación** | Logs, health, métricas, alertas, runbook y diagnóstico. |
| **Documentación** | PRD, arquitectura, seguridad, despliegue, operación y troubleshooting actualizados. |

---

## 4. Arquitectura objetivo on-premise

**Topología de referencia:**
$$\text{Usuarios/Agentes} \rightarrow \text{Reverse Proxy/IIS HTTPS} \rightarrow \text{TitanMDM Web/API} \rightarrow \text{SQL Server + Storage + Workers + SignalR/Remote + Python Ponches} \rightarrow \text{BioTime/Edge Connectors} \rightarrow \text{relojes y recursos locales.}$$

| Capa | Componente | Responsabilidad |
| :--- | :--- | :--- |
| **Acceso** | IIS/reverse proxy | TLS, hostnames, request limits, certificados, redirección HTTPS, cabeceras y publicación controlada. |
| **Aplicación** | TitanMDM API .NET | Identidad, RBAC, dispositivos, políticas, helpdesk, reportes, seguridad, remote orchestration y APIs. |
| **UI** | React/TypeScript | Portal único por rol, módulo Windows/Android/Helpdesk/Ponches/Admin. |
| **Datos** | SQL Server | Persistencia transaccional, EF Core migrations, índices, backup, HA si aplica. |
| **Tiempo real** | SignalR | Heartbeats/eventos/remote coordination con estrategia de escala local. |
| **Ponches / Localidades** | Python/FastAPI + Titan Edge Connector | Integración BioTime/ZKTeco y lógica especializada.<br>Canal saliente seguro, cola offline, acceso a recursos LAN, diagnóstico y sincronización. |
| **Artefactos** | Repositorio local/SMB u object storage compatible | Agentes, instaladores, paquetes software, adjuntos y reportes. |
| **Observabilidad**| OpenTelemetry + collector/plataforma corporativa | Logs, métricas, trazas, dashboards y alertas. |

---

## Fase 0 - PRD, arquitectura y gobierno
**Criticidad:** P0 - Bloqueante  
*Congelar el alcance Enterprise 1.0 y convertir decisiones técnicas en artefactos mantenibles.*

### Trabajo incluido
* Crear PRD.md por módulo con actores, reglas, flujos, errores, seguridad, SLA y criterios de aceptación.
* Crear ARCHITECTURE.md, SECURITY_ARCHITECTURE.md, THREAT_MODEL.md, DATABASE_ARCHITECTURE.md y ADRs.
* Definir matriz de ambientes Development/Testing/Staging/Production.
* Definir versionado semántico y política de release/change management.

### Entregables obligatorios
* PRD.md
* ARCHITECTURE.md
* SECURITY_ARCHITECTURE.md
* THREAT_MODEL.md
* ADR iniciales
* RELEASE_CHECKLIST.md

### Gate de salida
No se agregan nuevas funciones fuera del PRD sin cambio aprobado; todas las fases posteriores apuntan a criterios de aceptación documentados.

---

## Fase 1 - Higiene del repositorio y supply chain
**Criticidad:** P0 - Bloqueante  
*Eliminar exposiciones y establecer una cadena de compilación confiable.*

### Trabajo incluido
* Eliminar binarios, outputs, IDE metadata, env, backups y archivos accidentales del repositorio.
* Revisar historial Git, rotar secretos comprometidos y eliminar datos sensibles históricos.
* Configurar Dependabot, CodeQL, secret scanning, SCA y SBOM.
* Crear pipelines separados para backend, frontend, Python, agentes Windows y Android.

### Entregables obligatorios
* `.gitignore` definitivo
* `.github/workflows`
* SBOM por release
* Informe de secretos rotados
* Política de artefactos

### Gate de salida
Repository scan sin secretos activos ni artefactos sensibles; build reproducible desde checkout limpio.

---

## Fase 2 - Configuración y secretos
**Criticidad:** P0 - Bloqueante  
*Eliminar valores locales/hardcoded y preparar portabilidad on-prem/Azure.*

### Trabajo incluido
* Externalizar URLs, CORS, hosts, puertos, rutas, claves y credenciales.
* Usar Windows Credential Manager/DPAPI, variables protegidas o vault corporativo; definir adaptador compatible con Azure Key Vault.
* Crear appsettings por ambiente y validación de configuración al iniciar.
* Deshabilitar detailed errors y documentación de desarrollo en producción.

### Entregables obligatorios
* Configuration schema
* Production template
* Secret provider abstraction
* Startup validation

### Gate de salida
Ningún secreto o endpoint corporativo requerido queda compilado o hardcoded en el código.

---

## Fase 3 - Datos y bootstrap automatizado
**Criticidad:** P0 - Bloqueante  
*Garantizar que una instalación nueva construya el esquema correcto sin pasos manuales.*

### Trabajo incluido
* Consolidar modelo de datos y migrations EF Core.
* Crear migrador transaccional con lock y versión de esquema.
* Crear seed mínimo: organización, permisos base y proceso seguro de primer administrador.
* Agregar índices, constraints, relaciones, retención y mantenimiento.
* Probar install vacío, upgrade $N-1 \rightarrow N$ y rollback/restore.

### Entregables obligatorios
* DB migration package
* Seed controlado
* DB_SCHEMA.md
* Backup/restore scripts
* Pruebas de migración

### Gate de salida
Servidor nuevo puede crear o actualizar TitanMDM DB automáticamente y pasar pruebas de integridad.

---

## Fase 4 - Identidad, Entra y RBAC granular
**Criticidad:** P0 - Bloqueante  
*Cerrar el modelo de autenticación/autorización antes de exponer acciones administrativas.*

### Trabajo incluido
* Completar Entra ID/OIDC, logout, revocación y sesiones.
* Implementar rate limiting, lockout configurable y cuenta break-glass documentada.
* Definir permisos atómicos y scopes Organization/Site/Department/Group.
* Aplicar autorización server-side a cada endpoint y acción sensible.
* Auditar cambios de roles/permisos.

### Entregables obligatorios
* RBAC_MATRIX.md
* Permission catalog
* Auth tests
* Break-glass procedure

### Gate de salida
Pruebas demuestran que un usuario no puede acceder a datos/acciones fuera de su rol y scope.

---

## Fase 5 - Multi-site y modelo de 9 localidades
**Criticidad:** P0/P1  
*Hacer que sitios y ubicaciones sean parte del dominio, no solo filtros visuales.*

### Trabajo incluido
* Crear entidades Site/Location y relaciones con usuarios, equipos, tickets, relojes y conectores.
* Dashboards y reportes filtrables por sitio.
* Asignación Helpdesk por sitio/área.
* Políticas y aplicaciones asignables por sitio/grupo.

### Entregables obligatorios
* Site management UI/API
* Scope tests
* Site dashboards

### Gate de salida
Las nueve localidades pueden operar con segregación lógica y administración central.

---

## Fase 6 - Windows Devices, Groups e Inventory
**Criticidad:** P1 - Crítico  
*Llevar W1-W3 a experiencia enterprise estable.*

### Trabajo incluido
* Cerrar grupos estáticos/dinámicos, acciones masivas y filtros.
* Inventario gráfico y normalizado: hardware, OS, software, procesos, servicios, red, discos y usuarios.
* Crear Device Timeline unificando auditoría, policy, app, security, remote y helpdesk.
* Agregar paginación, server-side filters y exportación.

### Entregables obligatorios
* Device Center completo
* Device Timeline
* Inventory tests

### Gate de salida
Un técnico puede diagnosticar un dispositivo sin JSON crudo y rastrear cambios históricos.

---

## Fase 7 - Aplicaciones y catálogo empresarial
**Criticidad:** P1 - Crítico  
*Convertir Aplicaciones en un sistema de software deployment gestionado.*

### Trabajo incluido
* Catálogo con versión, arquitectura, hash, silent install/uninstall, detection rules y dependencias.
* Asignación por device/group/site.
* Estados Queue/Download/Install/Success/Fail/Retry.
* Rollout rings, scheduling, maintenance windows y rollback cuando sea posible.
* Verificación SHA-256/firma antes de ejecutar.

### Entregables obligatorios
* Application Catalog
* Package manifest
* Deployment engine
* Rollout UI

### Gate de salida
Instalar/desinstalar/actualizar un paquete en piloto y producción deja estado, logs y auditoría verificables.

---

## Fase 8 - Security Center, Compliance y Patch
**Criticidad:** P1 - Crítico  
*Elevar seguridad/compliance a control técnico real.*

### Trabajo incluido
* Controles Defender, Firewall, BitLocker, TPM, Secure Boot, updates, admins locales y software no autorizado.
* Security baselines y compliance evidence.
* Remediación manual/automática con permisos.
* Patch Management Windows: discovery, approval, maintenance window, reboot y compliance.

### Entregables obligatorios
* Security Center
* Baseline engine
* Patch dashboard
* Remediation audit

### Gate de salida
Cada estado de seguridad muestra evidencia, valor esperado, remediación y resultado.

---

## Fase 9 - Automation Engine
**Criticidad:** P1  
*Permitir respuesta automatizada segura y trazable.*

### Trabajo incluido
* Modelo Trigger-Condition-Action-Escalation.
* Acciones: comando, policy, limpieza, ticket, alerta, software y notificación.
* Protección contra loops, max executions y circuit breakers.
* Dry-run y approval para acciones de alto impacto.

### Entregables obligatorios
* Automation engine
* Rule builder
* Execution history

### Gate de salida
Reglas de prueba ejecutan y revierten de forma controlada sin loops ni bypass de RBAC.

---

## Fase 10 - Windows Agent y RemoteHost Enterprise
**Criticidad:** P0 - Bloqueante  
*Cerrar W7-W11 y convertir los binarios en componentes administrables a gran escala.*

### Trabajo incluido
* Enrollment de un solo uso, identidad persistente segura y rotación de credenciales.
* Heartbeat, reconnect, backoff, offline queue y command acknowledgement.
* Servicios con recovery, logs y watchdog.
* Self-update con firma/hash, rings, rollback, repair y uninstall.
* RemoteHost: multi-monitor, cursor, clipboard, calidad adaptativa, UAC/Secure Desktop y reboot-reconnect.
* Control lease para evitar doble control simultáneo.

### Entregables obligatorios
* Agent 1.0
* RemoteHost 1.0
* Updater
* GPO package
* Remote test suite

### Gate de salida
Equipo limpio puede instalar silenciosamente, enrolar, ejecutar comandos, soportar caídas, actualizarse y desinstalarse.

---

## Fase 11 - Helpdesk Enterprise
**Criticidad:** P1 - Crítico  
*Completar ITSM y su integración con endpoint management.*

### Trabajo incluido
* Estados completos, reapertura, prioridad Impact x Urgency, SLA y pausas.
* Autoasignación por sitio, especialidad, grupo y carga.
* Correo bidireccional, threading, adjuntos y deduplicación.
* KPIs por agente/sitio y portal simplificado no-TI.
* Acciones contextuales desde ticket: dispositivo, timeline, remote y software.

### Entregables obligatorios
* Workflow Helpdesk
* SLA engine
* Routing engine
* Mail integration
* KPIs

### Gate de salida
Casos E2E de correo $\rightarrow$ ticket $\rightarrow$ asignación $\rightarrow$ remote $\rightarrow$ resolución $\rightarrow$ reapertura pasan con auditoría.

---

## Fase 12 - Ponches, Python y BioTime
**Criticidad:** P0 - Bloqueante  
*Consolidar el servicio Python como componente oficial y eliminar integraciones frágiles.*

### Trabajo incluido
* Una sola fuente de código Python; dependencies lock.
* Bridge interno autenticado y health real de BioTimeDB/relojes.
* Eliminar usuarios locales JSON si la identidad Titan puede cubrirlos.
* Proteger archivos Fiorella/exportaciones y endpoints auxiliares.
* Servicio Python con recovery, logs y upgrade.

### Entregables obligatorios
* TitanPonchesService
* requirements/lock
* Ponches API contract
* Health checks
* Sync tests

### Gate de salida
Titan puede iniciar, autenticar y verificar Ponches; sincronización bidireccional y exportaciones pasan pruebas.

---

## Fase 13 - Titan Edge Connector
**Criticidad:** P0/P1  
*Soportar nueve redes/localidades sin exponer recursos LAN al exterior.*

### Trabajo incluido
* Servicio Windows por localidad con identidad propia y mTLS/token rotativo.
* Conexiones salientes, local queue, store-and-forward y retry.
* Discovery/health de BioTime, relojes y futuras dependencias locales.
* Remote diagnostics y self-update.

### Entregables obligatorios
* Edge Connector 1.0
* Site enrollment
* Offline queue
* Diagnostics

### Gate de salida
Una localidad puede perder WAN y sincronizar nuevamente sin perder eventos.

---

## Fase 14 - Android Enterprise
**Criticidad:** P1  
*Finalizar agente y administración Android para uso corporativo.*

### Trabajo incluido
* Enterprise binding, QR enrollment, fully managed/dedicated/work profile según caso.
* Apps, kiosk, restricciones, compliance, lost mode, location, lock/wipe.
* Managed Google Play, certificates y Wi-Fi cuando aplique.
* Agent commands y sincronización comprobados.

### Entregables obligatorios
* Android Agent 1.0
* Enrollment guide
* Policy catalog
* Android E2E tests

### Gate de salida
Dispositivo de prueba completa enrollment, policy, app, command, compliance y retiro.

---

## Fase 15 - Reportes, auditoría y IA gobernada
**Criticidad:** P1  
*Cerrar capacidades transversales de reporting, trazabilidad y asistentes.*

### Trabajo incluido
* Report builder por site/department/group/date/status.
* PDF/XLSX/CSV y scheduled reports.
* Audit trail Who/What/When/Target/Before/After/Result/CorrelationId.
* Titan Assistant y Fiorella limitados por permisos, scope, confirmation y tool audit.

### Entregables obligatorios
* Report center
* Immutable audit model
* AI tool policy

### Gate de salida
Toda acción sensible es trazable y los asistentes no pueden exceder los permisos del usuario.

---

## Fase 16 - UI/UX y Design System
**Criticidad:** P1  
*Unificar experiencia final sin romper funcionalidad.*

### Trabajo incluido
* Design tokens, componentes comunes, responsive y accesibilidad.
* Estados loading/empty/error/success consistentes.
* Modernizar Windows, Security, Reports, Apps, Helpdesk y Ponches.
* Validar 1366x768, 1080p, 1440p, 4K y tablet.

### Entregables obligatorios
* Titan Design System
* UI regression suite
* Accessibility checklist

### Gate de salida
No existen cambios visuales que eliminen funciones o rompan navegación; pruebas de regresión visual aprobadas.

---

## Fase 17 - Installer Server on-premise
**Criticidad:** P0 - Bloqueante  
*Construir el instalador completo solicitado para despliegue empresarial reproducible.*

### Trabajo incluido
* Preflight de SO, CPU, RAM, disco, puertos, permisos y conectividad.
* Instalar/verificar .NET Runtime, Hosting Bundle, Python, VC runtimes y dependencias necesarias.
* Configurar IIS, bindings HTTPS, certificados, application pools y ACLs.
* Configurar SQL Server existente o modo SQL dedicado según política; crear DB y ejecutar migrations.
* Crear servicio Python Ponches, workers, updater y tareas necesarias.
* Configurar storage paths, logs, backups y firewall.
* Wizard para organization, Entra, mail, BioTime y ubicaciones.
* Health validation final y rollback transaccional si falla.

### Entregables obligatorios
* TitanMDM-Server-Setup.exe
* Silent install switches
* Answer file
* Rollback/uninstall
* INSTALLATION_GUIDE.md

### Gate de salida
En Windows Server limpio/soportado, el instalador deja Titan operativo sin ejecutar pasos manuales no documentados.

---

## Fase 18 - Agent/Edge installers y firma
**Criticidad:** P0 - Bloqueante  
*Distribuir endpoints con cadena de confianza empresarial.*

### Trabajo incluido
* TitanMDM-Agent-Setup.exe con GUI/silent/GPO/repair/upgrade/uninstall.
* TitanMDM-Edge-Setup.exe con site enrollment.
* Firmar EXE/MSI/scripts/binarios con certificado corporativo de code signing.
* Manifest de release con SHA-256 y versión.

### Entregables obligatorios
* Agent Setup
* Edge Setup
* Code signing pipeline
* Release manifest

### Gate de salida
Defender/SmartScreen y controles corporativos validan binarios firmados y el despliegue masivo funciona.

---

## Fase 19 - Observabilidad y operaciones
**Criticidad:** P0  
*Hacer la plataforma diagnosticable y operable 24x7.*

### Trabajo incluido
* Structured logging + correlation IDs.
* Health `/live`, `/ready` y dependency health.
* Métricas de API, SQL, agentes, commands, SignalR, tickets, Python y Edge.
* Alertas y dashboards en plataforma corporativa/OpenTelemetry.
* Runbooks para incidentes frecuentes.

### Entregables obligatorios
* OPERATIONS_RUNBOOK.md
* Monitoring dashboards
* Alerts
* Health endpoints

### Gate de salida
Un operador puede detectar rápidamente qué dependencia falló y seguir un runbook de recuperación.

---

## Fase 20 - Backup, HA y Disaster Recovery
**Criticidad:** P0  
*Proteger datos y definir recuperación realista.*

### Trabajo incluido
* SQL full/diff/log backups según RPO.
* Backup de attachments, packages, configs, certificados y manifests.
* Pruebas de restore automatizadas.
* Definir RPO/RTO, SQL HA opcional, almacenamiento redundante y procedimiento de reconstrucción.

### Entregables obligatorios
* BACKUP_RESTORE.md
* DISASTER_RECOVERY.md
* Restore test evidence

### Gate de salida
Restauración completa se prueba en ambiente aislado dentro del RTO acordado.

---

## Fase 21 - Pruebas unitarias, integración y E2E
**Criticidad:** P0  
*Convertir calidad en gate automático antes de cada release.*

### Trabajo incluido
* Unit tests para auth, RBAC, policies, commands, compliance, SLA, routing y automation.
* Integration tests API-SQL, API-Python, mail, storage, Agent y Edge.
* Playwright E2E para flujos críticos.
* Migración desde DB vacía y upgrade N-1.

### Entregables obligatorios
* TEST_PLAN.md
* Automated suites
* Coverage report
* Regression catalog

### Gate de salida
Pipeline bloquea releases cuando falla cualquier prueba crítica.

---

## Fase 22 - Seguridad ofensiva y hardening final
**Criticidad:** P0  
*Validar el producto como servicio administrativo privilegiado.*

### Trabajo incluido
* Threat model final.
* SAST/SCA/DAST.
* Tests IDOR, auth bypass, privilege escalation, command injection, upload y remote session hijacking.
* Hardening IIS/Windows/SQL/Python y service accounts.
* Remediar vulnerabilidades críticas/altas antes de producción.

### Entregables obligatorios
* Security test report
* Hardening baseline
* Risk acceptance log

### Gate de salida
Sin vulnerabilidades críticas/altas abiertas salvo aceptación formal excepcional.

---

## Fase 23 - Carga, resiliencia y performance
**Criticidad:** P0  
*Dimensionar para operación real de nueve localidades.*

### Trabajo incluido
* Simular operadores, heartbeats, comandos, tickets, remote sessions y reportes concurrentes.
* Medir p50/p95/p99, CPU, RAM, SQL, colas y errores.
* Chaos/failure tests: SQL/Python/Edge/WAN/service restart.
* Optimizar índices, caching, paginación y límites.

### Entregables obligatorios
* PERFORMANCE_PLAN.md
* Load results
* Capacity baseline

### Gate de salida
Capacidad objetivo aprobada con margen y sin pérdida silenciosa de eventos.

---

## Fase 24 - Staging, piloto y producción
**Criticidad:** P0  
*Entrar a producción progresivamente y con rollback.*

### Trabajo incluido
* Crear staging equivalente.
* Pilotar TIC, 5-10 endpoints, luego una localidad y expandir por waves.
* Checklist pre-go-live, freeze window y rollback.
* Capacitación a TIC y usuarios Helpdesk.

### Entregables obligatorios
* STAGING_GUIDE.md
* PILOT_PLAN.md
* GO_LIVE_CHECKLIST.md
* Training pack

### Gate de salida
Piloto estable y criterios de aceptación empresariales firmados antes de expandir a nueve localidades.

---

## 25. Requisitos del instalador on-premise

| Etapa | Acción automatizada |
| :--- | :--- |
| **Preflight** | OS soportado, privilegios, memoria/disco, puertos, DNS, TLS, SQL y conectividad. |
| **Prerequisitos** | .NET/Hosting Bundle, Python runtime/venv, runtimes nativos y herramientas estrictamente necesarias. |
| **IIS** | Site/AppPool, HTTPS, bindings, ACLs, request limits, compression, logging y hardening. |
| **SQL** | Conectividad, creación de base, migrations, índices, seed y validación de versión. |
| **Titan API/UI** | Publicar binarios versionados, config por ambiente y servicio/update strategy. |
| **Ponches** | Instalar Python service, dependencies, integration secret/certificate y health. |
| **Storage** | Directorios/SMB/object store para packages, attachments, reports y logs. |
| **Seguridad** | Certificados, service accounts, DPAPI/vault, firewall rules y mínimo privilegio. |
| **Validación** | Health de SQL/API/UI/Python/SignalR/Mail/Storage; rollback si falla. |
| **Salida** | Informe de instalación, versiones, URLs, servicios, backups y acciones pendientes. |

---

## 26. Base de compatibilidad futura Azure / Híbrida

* No usar rutas de disco como contrato de dominio: encapsular almacenamiento en `IFileStorage` para poder cambiar a Azure Blob.
* No acoplar secretos a DPAPI exclusivamente: `ISecretProvider` debe admitir proveedor local y Azure Key Vault.
* No asumir un solo proceso SignalR: abstraer backplane/real-time para migrar a Azure SignalR.
* Usar configuration providers estándar y environment variables para poder ejecutar API/Python en App Service/containers.
* Mantener migrations independientes del instalador gráfico para ejecutarlas en pipelines cloud.
* Edge Connector debe usar HTTPS/mTLS saliente y funcionar igual contra endpoint on-premise o Azure.
* Telemetry mediante OpenTelemetry para exportar a collector local o Application Insights sin reescribir código.
* Entra ID y RBAC deben ser idénticos en ambos modelos.

---

## 27. Orden práctico para continuar el desarrollo

| Macro sprint | Contenido | Bloquea |
| :---: | :--- | :--- |
| **A** | PRD + repo security + configuración + DB | Todo lo demás |
| **B** | Entra/RBAC + Sites | Módulos administrativos |
| **C** | Windows W1-W6 + Apps + Security + Reports | Cierre funcional Windows |
| **D** | Agent + RemoteHost + UAC + W7-W11 | Producción Windows |
| **E** | Helpdesk | ITSM |
| **F** | Ponches + Python + Edge | Multi-localidad/BioTime |
| **G** | Android Enterprise | UEM completo |
| **H** | Automation + Patch + Timeline + AI | Madurez avanzada |
| **I** | UI/UX final | Release candidate |
| **J** | Installers + signing + updater | Distribución |
| **K** | Observability + backup/DR | Operación |
| **L** | Tests + security + load + resilience | Release gate |
| **M** | Staging + pilot + production | Go-live |

---

## 28. Documentación final obligatoria

* PRD.md
* ARCHITECTURE.md
* SECURITY_ARCHITECTURE.md
* THREAT_MODEL.md
* DATABASE_ARCHITECTURE.md
* RBAC_MATRIX.md
* API_CONTRACTS.md
* TEST_PLAN.md
* PERFORMANCE_PLAN.md
* INSTALLATION_GUIDE.md
* DEPLOYMENT_ONPREM.md
* OPERATIONS_RUNBOOK.md
* BACKUP_RESTORE.md
* DISASTER_RECOVERY.md
* INCIDENT_RESPONSE.md
* PILOT_PLAN.md
* GO_LIVE_CHECKLIST.md
* CHANGELOG.md

---

## Conclusión

TitanMDM on-premise debe convertirse en un producto instalable y administrable, no en un conjunto de proyectos que funcionan solamente en el entorno del desarrollador. La arquitectura aquí definida prioriza el servidor corporativo local, las nueve localidades, el control de endpoints y la integración BioTime, pero conserva contratos técnicos para migrar componentes a Azure sin rediseñar el dominio. El criterio de éxito será poder reconstruir, actualizar, auditar, respaldar y recuperar la plataforma mediante procedimientos repetibles y comprobados.