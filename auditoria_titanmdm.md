# TitanMDM
## Auditoría técnica, comparativa ManageEngine y hoja de ruta

**Repositorio:** github.com/Elias Hughes/mdmTitan  
**Commit revisado:** ec5ec02 (rama main)  
**Fecha del informe:** 7 de octubre de 2026  

---

## 1. Resumen ejecutivo

Revisé el repositorio completo (clonado, 1.046 archivos), el Plan Maestro On-Premise que adjuntaste y el código de los módulos más críticos: arquitectura backend, escritorio remoto (agente, hub y visor), Helpdesk, automatización, Android, robot de login y asistente. Esta es una revisión estática: no compilé ni ejecuté la aplicación, así que los diagnósticos de runtime (por ejemplo, por qué falla el ratón remoto) son hipótesis fuertes respaldadas por el código, no pruebas medidas.

**Nivel actual estimado: 52/100 - "plataforma funcional avanzada, aún no producto empresarial".**

Tiene un alcance funcional enorme para un proyecto individual (~227.000 líneas, 60 controladores, 35 migraciones, agentes Windows y Android, Helpdesk con enrutamiento por sitio/especialidad, RBAC con scopes, automatización, ponches). Lo que lo separa de ManageEngine no es la cantidad de módulos sino la profundidad, la fluidez y la operabilidad: escritorio remoto lento, ausencia de patch management real, Helpdesk sin Problem/Change/Knowledge Base, nada de observabilidad ni health checks, pruebas casi inexistentes en frontend, y una arquitectura que se está desviando de Clean Architecture.

### Lo mejor que has logrado
* **Documentación y gobierno sorprendentemente buenos:** PRD de 609 líneas, ARCHITECTURE, SECURITY_ARCHITECTURE, THREAT_MODEL, RBAC_MATRIX, BREAK_GLASS y 4 ADRs. Pocos proyectos de este tamaño los tienen.
* **Higiene del repo:** sin .env, binarios, llaves ni credenciales en el árbol; CI separado para backend, frontend y ponches, CodeQL, SBOM, Dependabot y política de artefactos.
* **Modelo de seguridad real:** Entra ID, handlers de autorización por permiso y por scope (Organization/Site), pruebas de segregación por sitio, control lease para evitar doble control remoto.
* **Multi-sitio como dominio, no como filtro:** Sites, cobertura de Helpdesk por sitio, ranking de cobertura, tickets con reglas de dominio probadas (28 tests).
* **Motor de movimiento del asistente con física (velocidad, fricción, rAF, máquina de estados):** es una base mejor de lo que parece para lograr movimiento natural.

### Lo que más duele hoy (resumen)
* **Escritorio remoto:** captura GDI + JPEG completo + base64 dentro de JSON + 2 consultas a BD por cada movimiento de ratón. Explica la lentitud y buena parte de la falta de control (sección 7).
* **Clean Architecture:** 30 de 60 controladores (y el hub remoto) usan el DbContext directamente; la capa Application tiene solo ~3.300 líneas frente a 38.000 en la capa Api. La lógica vive en controladores y servicios de Infraestructura.
* **Archivos gigantes:** páginas React de 2.000-3.200 líneas, hub de 1.934 líneas, controladores de 1.400-1.850. Viola SRP y hace imposible mantener la "fluidez" tipo ManageEngine.
* **Producción:** 0 health checks, 0 rate limiting, 0 OpenTelemetry/Serilog, 0 backups scripts, 0 code signing, Edge Connector inexistente, RequireHttps=false y EnableDetailedErrors=true en el appsettings base, IP interna hardcodeada.
* **Una sola app:** Ponches aún es casi otra aplicación (frontend propio de 62 archivos, usuarios en JSON, backend Python con login aparte). El asistente de IA usa OpenRouter "free" aunque tu ADR-004 dice Ollama local.

---

## 2. Radiografía del repositorio

| Componente | Archivos | Líneas | Comentario |
| :--- | :--- | :--- | :--- |
| **Backend Api** | 94 | 38.009 | Demasiado grande para una capa de presentación: contiene lógica de negocio (Helpdesk Reports 1.414, Ponches 1.856, RemoteSessions 1.443, EntraLogin 1.410). |
| **Backend Application** | 82 | 3.263 | Delgadísima. Solo 2 interfaces en /Interfaces; no hay casos de uso/handlers claros. |
| **Backend Domain** | 72 | 9.501 | Sin dependencias externas (bien). Tickets con comportamiento y tests; el resto tiende a anémico. |
| **Backend Infrastructure** | 113 | 32.926 | Contiene la lógica real (HelpdeskService en 5 partial classes, Dashboard 1.456, Reports 1.234, Seeder 1.512). |
| **Frontend React/TS** | 216 | 94.961 | Muy productivo pero con páginas monolíticas; solo 7 componentes UI base; 7 CSS globales; 0 tests. |
| **Agentes (Windows + Android)** | 89 | 20.987 | WindowsAgent y RemoteHost (.NET), agente Android Kotlin (37 archivos). |
| **Ponches (Python + front propio)** | 116 | 24.124 | FastAPI + frontend duplicado + datos JSON. Aún no está fusionado de verdad. |
| **Pruebas** | 14 | 2.299 | 94 pruebas backend (helpdesk, auth, sitios). Proyecto de integración con solo un test placeholder. 0 pruebas de frontend, 0 E2E. |

### Archivos más grandes (señal de deuda de diseño)

| Archivo | Líneas | Problema |
| :--- | :--- | :--- |
| `pages/policies/PolicyEditorPage.tsx` | 3.217 | Un solo componente con editor completo de políticas. |
| `pages/helpdesk/HelpdeskSpecialtiesPage.tsx` | 2.941 | Pantalla de configuración con todo mezclado (estado, API, UI). |
| `pages/helpdesk/HelpdeskCenterPage` / `OperationsPage` / `ReportsPage` | 2.286 / 2.249 / 2.023 | Los módulos principales de la mesa de ayuda. |
| `Api/Hubs/RemoteSupportHub.cs` | 1.934 | Hub con sesiones, leases, monitores, frames, input y consultas EF. |
| `Api/Controllers/PonchesController.cs` | 1.856 | Proxy/lógica de ponches dentro de un controlador. |
| `Infrastructure/Helpdesk/HelpdeskService.Routing.cs` | 1.661 | Se usa partial class para esconder una clase de 5.000+ líneas. |

---

## 3. Calificación por área

*Escala:* $\ge65$ verde (sólido), 40-64 amarillo (funciona pero con deuda), <40 rojo (bloqueante para producción empresarial). La nota global pondera funcionalidad y calidad de ingeniería por igual.

| Área | Nota | Lectura |
| :--- | :--- | :--- |
| **Alcance funcional** | $74/100$ | Cubre casi todo el mapa del Plan Maestro a nivel de pantallas y APIs. |
| **Documentación / gobierno** | $70/100$ | PRD, ADRs, threat model y RBAC matrix existen. Faltan DB, API contracts, test plan, runbooks. |
| **Seguridad (diseño)** | $62/100$ | Buen modelo; falta rate limiting, config segura por defecto, secret provider y code signing. |
| **Arquitectura backend (Clean)** | $48/100$ | Dependencias de proyecto correctas, pero controladores acoplados a EF y Application casi vacía. |
| **Arquitectura frontend** | $42/100$ | TypeScript limpio (solo 1 any) pero páginas gigantes, CSS global y poca adopción de React Query. |
| **SOLID / código limpio** | $40/100$ | SRP e DIP son las violaciones principales: Program.cs de 648 líneas. |
| **Windows (vs Endpoint Central)** | $58/100$ | Inventario, políticas, kiosko, acciones, seguridad; sin patch management completo ni timeline. |
| **Android (vs Endpoint Central)** | $48/100$ | Agente con DevicePolicyManager y workers; Android Enterprise aún por cerrar. |
| **Mesa de ayuda (vs ServiceDesk Plus)** | $63/100$ | Incidentes/solicitudes, SLA, enrutamiento y correo muy avanzados; sin Problem/Change/KB/CMDB. |
| **Escritorio remoto** | $28/100$ | Funciona como demo; rendimiento y control de entrada no son de producción. |
| **Automatización** | $50/100$ | Motor reglas/ejecuciones existe; faltan dry-run, circuit breakers y ventanas de mantenimiento. |
| **Pruebas / calidad** | $25/100$ | ~94 tests backend útiles; nada en frontend/E2E/integración. |
| **Observabilidad / operación** | $15/100$ | Sin health checks, métricas, logging estructurado ni backups automatizados. |
| **UX / integración como una sola app** | $50/100$ | Shell y design tokens existen; módulos con estilos propios y Ponches separado. |

**Nota global: $52/100$**  
Con 4-5 fases bien ejecutadas (escritorio remoto, arquitectura, observabilidad+pruebas, automatización de Helpdesk, patching) puedes subir a ~75 sin reescribir nada.

---

## 4. Clean Architecture, SOLID y código limpio

### 4.1 Qué está bien
* Dirección de dependencias entre proyectos correcta: Domain no referencia a nadie; Application $\rightarrow$ Domain; Infrastructure $\rightarrow$ Application + Domain; Api compone todo (composition root).
* Domain sin frameworks (solo System.*), con reglas probadas (Helpdesk TicketDomain Tests, Site Domain Tests, DeviceNameParserTests).
* ADR-002 (monolito modular) es la decisión correcta para on-premise con 9 sedes: no necesitas microservicios.
* TypeScript estricto en la práctica: solo 1 uso de any en 95.000 líneas; carpeta api/ separada por módulo.

### 4.2 Dónde se rompe

| Principio | Estado | Evidencia y consecuencia |
| :--- | :--- | :--- |
| **Regla de dependencia (la UI no conoce la persistencia)** | PARCIAL | 30 de 60 controladores + RemoteSupportHub inyectan TitanMdmDbContext directamente (Helpdesk*, Users, Roles, RemoteSessions, VirtualAgent...). La capa Api depende de EF Core. |
| **Capa Application / casos de uso** | PENDIENTE | 3.263 líneas en total y solo IAuthenticationService e ITokenService en /Interfaces. No hay handlers (comando/consulta). La lógica de negocio terminó en Infrastructure y en controladores. |
| **S Responsabilidad única** | PENDIENTE | RemoteSupportHub (1.934 líneas) mezcla seguridad, sesiones, leases, monitores, frames y BD. Páginas React de 2.000-3.200 líneas. HelpdeskService dividido en 5 partial en vez de en servicios cohesivos. |
| **O Abierto/cerrado** | PARCIAL | Automatización y enrutamiento por reglas ayudan, pero acciones/comandos del agente se extienden editando CommandExecutor (744 líneas) en lugar de registrar handlers. |
| **L Sustitución de Liskov** | LOGRADO | No se detectaron jerarquías de herencia problemáticas; se usa composición. |
| **I Segregación de interfaces** | PARCIAL | Hay pocas interfaces; las dependencias son clases concretas grandes (DbContext, HelpdeskService). |
| **D Inversión de dependencias** | PARCIAL | Auth y tokens sí abstraídos. No existen IFileStorage, ISecretProvider ni backplane abstracto que pide tu propio plan (Sección 26) $\rightarrow$ bloquea la ruta Azure/híbrida. |
| **Program.cs** | PARCIAL | 648 líneas de registro. Debe dividirse en extensiones por módulo (AddHelpdeskModule(), AddRemoteModule()...). |
| **Reglas arquitectónicas automatizadas** | PENDIENTE | Sin NetArchTest/ArchUnit: nada impide que mañana Domain referencie Infrastructure. |

### 4.3 Cómo corregirlo sin reescribir (estrategia incremental)
* **Regla de oro:** ningún controlador nuevo toca el DbContext. Controlador $\rightarrow$ IRequestHandler/servicio de aplicación $\rightarrow$ repositorio o IQuery.
* **Vertical slices por módulo** (Helpdesk, Devices, Remote, Policies, Apps, Ponches): cada módulo con Commands/, Queries/, Validators/, Dtos/. Empieza por Remote (es el que más duele) y luego Helpdesk.
* **Mover EF a Infrastructure** detrás de interfaces de consulta (IDeviceQueries, ITicketQueries). Para lecturas puras puedes mantener proyecciones EF dentro de la query concreta.
* **Partir los gigantes:** regla práctica máx. 400 líneas por archivo, 40 por método. Frontend: página = composición de hooks + componentes de sección (el patrón que ya usaste en groups/hooks y devices/hooks es el correcto).
* Agregar pruebas de arquitectura y un análisis de complejidad ciclomática al CI para que la deuda no vuelva.
* **Abstracciones del Plan Maestro:** IFileStorage, ISecretProvider, IRealtimeBroadcaster con implementación local primero.

---

## 5. Auditoría contra el Plan Maestro (fases 0 a 24)

*Leyenda:* VERDE = logrado | AMARILLO = parcial | ROJO = pendiente. Verifiqué con búsquedas en el código; donde no encontré evidencia lo marco como pendiente y lo indico (puede existir bajo otro nombre).

| Fase | Módulo | Estado | Lo que ya se consiguió (verde) | Lo que falta |
| :--- | :--- | :--- | :--- | :--- |
| **F0** | Gobierno / PRD | LOGRADO | PRD, ARCHITECTURE, SECURITY_ARCH, THREAT_MODEL, RBAC MATRIX, BREAK GLASS, ADR-001..004, RELEASE_CHECKLIST. | DATABASE ARCHITECTURE, API_CONTRACTS, TEST_PLAN, matriz de ambientes, versionado. ADR-004 (Ollama) contradice la config actual (OpenRouter). |
| **F1** | Higiene del repo | LOGRADO | Sin .env/binarios/claves en el árbol; workflows backend/frontend/ponches, CodeQL, SBOM, Dependabot; ARTIFACT_POLICY. | Auditar historial Git y rotar secretos; pipelines de agentes Windows y Android; confirmar secret scanning. |
| **F2** | Configuración y secretos | PARCIAL | Secciones Cors, Cache, AI; Data Protection usado; UseHsts y Forwarded Headers presentes. | IP 172.21.20.14 hardcodeada; RequireHttps=false, EnableDetailedErrors=true y AllowedHosts=* en el appsettings base; sin ISecretProvider ni ValidateOnStart. |
| **F3** | Datos y bootstrap | PARCIAL | 35 migraciones EF; DatabaseBootstrapper con MigrateAsync; Seeder (1.512 líneas); instalador.iss. | Migrador con lock/versionado, pruebas install vacío y N-1$\rightarrow$N, scripts backup/restore, DB_SCHEMA.md. |
| **F4** | Identidad / RBAC | PARCIAL | Entra login, handlers de permisos y scopes, tests de autorización, break-glass documentado. | Rate limiting (0 resultados), lockout configurable completo, revocación de sesiones, auditoría de cambios de rol verificada. |
| **F5** | Multi-sitio | LOGRADO | Módulo Sites, SiteScopeSegregationTests, cobertura y ranking de sitios en Helpdesk, scopes en autorización. | Dashboards por sitio y políticas/apps asignables por sitio (verificar profundidad). |
| **F6** | Windows: grupos e inventario | PARCIAL | Grupos estáticos/dinámicos, WindowsControlCenter, inventario (888 líneas), acciones y filtros. | Device Timeline (no encontrado), exportación, paginación server-side en todo, inventario 100% gráfico. |
| **F7** | Aplicaciones / catálogo | PARCIAL | SoftwareDeploymentService, WindowsSoftwareManager con verificación SHA-256, resultados de despliegue. | Rollout rings, maintenance windows (no encontrado), dependencias, rollback, firma. |
| **F8** | Security Center / Patch | PARCIAL | SecurityPostureService, WindowsSecurityProvider (BitLocker, Defender...), WindowsUpdateProvider. | Baselines con evidencia, remediación auditada, aprobación de parches, ventanas de mantenimiento, reinicios, dashboard de parches. |
| **F9** | Automation Engine | PARCIAL | AutomationRule/Execution, dispatcher de eventos, pantalla de reglas (1.607 líneas). | Dry-run, aprobaciones, circuit breakers, protección anti-loop, acción de ticket y de software. |
| **F10**| Agente + RemoteHost | PARCIAL | Servicio agente, enrollment, heartbeat, ejecutor de comandos, lease de control, multi-monitor, indicador de sesión. | Rendimiento y entrada del escritorio remoto, UAC/Secure Desktop, self-update con firma, watchdog, suite de pruebas remotas. |
| **F11**| Helpdesk Enterprise | PARCIAL | Tickets, SLA, enrutamiento por sitio/especialidad/carga, correo (worker + import), plantillas, cierre, seguimiento, reportes, portal MyHelpdesk. | Automatización total, Problem/Change/KB/CSAT, E2E correo ticket remote cierre, pausas SLA verificadas. |
| **F12**| Ponches / Python | PARCIAL | Servicio FastAPI, titan_bridge.py, ADR-003, módulo dentro del portal. | Usuarios en JSON (app_users.json), frontend duplicado, health real BioTime, lock de dependencias, auth del bridge. |
| **F13**| Titan Edge Connector | PENDIENTE | | No existe (0 resultados de EdgeConnector en código y docs). Bloquea las 9 localidades. |
| **F14**| Android Enterprise | PARCIAL | Agente Kotlin (DevicePolicyManager, WorkManager), AndroidEnterprise y AndroidDeviceSyncService, políticas. | QR enrollment E2E, kiosko, lost mode, managed Google Play, certificados/Wi-Fi, pruebas E2E. |
| **F15**| Reportes / auditoría / IA | PARCIAL | ReportsService, módulo Audit, VirtualAgent y asistente. | Audit inmutable con before/after, programación de reportes, política de herramientas de IA con confirmación. |
| **F16**| UI/UX Design System | PARCIAL | Tokens (titan-theme/motion/shell), 7 componentes UI, reduced-motion respetado. | Biblioteca de componentes real, regresión visual, accesibilidad, quitar CSS por módulo (a/b/c/d). |
| **F17**| Instalador servidor | PARCIAL | Setup-TitanMDM.iss y scripts de build. | Preflight, IIS, SQL, servicio Python, rollback transaccional y validación final (no verificados). |
| **F18**| Instaladores agente / firma | PARCIAL | Scripts de instalación, GPO y desinstalación del agente. | Firma de código (no hay pipeline), Edge Setup, release manifest con SHA-256. |
| **F19**| Observabilidad | PENDIENTE | Existen CorrelationId en 14 archivos y 11 BackgroundService. | Sin health checks, OpenTelemetry, Serilog, métricas, alertas ni runbooks. |
| **F20**| Backup/HA/DR | PENDIENTE | | Sin scripts de backup/restore ni documentación de DR. |
| **F21**| Pruebas | PARCIAL | ~94 tests backend (helpdesk, auth, sitios) y CI de build. | Integración, E2E Playwright, frontend, agentes, cobertura y gate en pipeline. |
| **F22**| Seguridad ofensiva | PENDIENTE | CodeQL activo. | DAST, pruebas IDOR/privilegios/hijacking de sesión remota, hardening IIS/SQL. |
| **F23**| Carga y resiliencia | PENDIENTE | | Sin pruebas de carga, chaos ni línea base de capacidad. |
| **F24**| Staging/piloto / prod | PENDIENTE | RELEASE CHECKLIST. | STAGING GUIDE, PILOT_PLAN, GO_LIVE_CHECKLIST, capacitación. |

**Resumen:** 3 fases logradas, 16 parciales, 6 pendientes de 25. Los pendientes (Edge, observabilidad, backup, carga, seguridad ofensiva, piloto) no son pantallas: son lo que convierte el proyecto en un producto. Es normal que vengan al final, pero hay que reservar tiempo explícito.

---

## 6. ¿Qué tan cerca estás de ManageEngine?

*Referencia:* Endpoint Central (Windows/Android/UEM) y ServiceDesk Plus (ITSM), tomando sus capacidades generales conocidas. No pretendo igualar todo: el objetivo realista es igualar el 20% de funciones que se usa el 80% del tiempo y superar a ManageEngine en lo que ellos no ofrecen (multi-sitio con BioTime/ponches, asistente integrado).

### 6.1 Windows vs Endpoint Central

| Capacidad | Estado | Qué falta para nivel ManageEngine |
| :--- | :--- | :--- |
| **Inventario hardware/software** | LOGRADO | Normalizar y agregar historial de cambios por dispositivo (diff entre inventarios). |
| **Grupos y acciones masivas** | LOGRADO | Previsualización de miembros de grupos dinámicos y cola de acciones con progreso. |
| **Políticas/configuraciones** | PARCIAL | Editor de 3.200 líneas funciona, pero faltan plantillas, versiones, comparación y rollback de políticas. |
| **Distribución de software** | PARCIAL | Rings, ventanas, dependencias, reintentos visibles, repositorio de paquetes con detección de instalación. |
| **Patch management** | PENDIENTE | Descubrimiento, aprobación, pruebas piloto, ventanas, reinicio, cumplimiento por sitio. Es la función estrella de Endpoint Central. |
| **Seguridad y cumplimiento** | PARCIAL | Baselines con evidencia/valor esperado/remediación; USB y control de aplicaciones. |
| **Control remoto** | PENDIENTE | Ver sección 7: rendimiento, entrada, chat, transferencia de archivos, grabación, consentimiento, UAC. |
| **Device 360 / línea de tiempo** | PENDIENTE | Una vista única con hardware, software, tickets, comandos, remoto y seguridad en cronología. |
| **Power / Wake-on-LAN / scripts** | PARCIAL | Biblioteca de scripts versionados con parámetros y resultados por equipo; WoL por sitio vía Edge. |
| **Informes y paneles** | PARCIAL | Reportes programados, exportación PDF/XLSX/CSV y dashboards por sitio. |

### 6.2 Android vs Endpoint Central MDM

| Capacidad | Estado | Qué falta |
| :--- | :--- | :--- |
| **Enrollment (QR / zero-touch / work profile)** | PARCIAL | Flujo QR extremo a extremo, perfiles totalmente gestionado / dedicado / trabajo, prueba E2E. |
| **Políticas y restricciones** | PARCIAL | Catálogo de políticas con plantillas y asignación por grupo/sitio, estado de aplicación por política. |
| **Aplicaciones (Managed Google Play)** | PENDIENTE | Catálogo, instalación silenciosa, actualización y bloqueo; apps privadas. |
| **Kiosko / dispositivo dedicado** | PARCIAL | Existe módulo de kiosko; falta salida segura, multi-app y reporte de estado. |
| **Lost mode, ubicación, geofencing** | PARCIAL | Módulos presentes; validar consumo de batería, frecuencia y privacidad. |
| **Cumplimiento y acciones remotas** | PARCIAL | Motor de compliance con acciones automáticas (lock, wipe selectivo) y auditoría. |

### 6.3 Mesa de ayuda vs ServiceDesk Plus

| Capacidad | Estado | Qué falta |
| :--- | :--- | :--- |
| **Tickets, estados, prioridad, SLA** | LOGRADO | Verificar pausas de SLA y calendarios laborales por sitio. |
| **Enrutamiento por sitio / especialidad / carga** | LOGRADO | Diferenciador fuerte; falta explicar al usuario por qué se asignó (ya existe diagnóstico). |
| **Correo bidireccional** | PARCIAL | Hilos, adjuntos y deduplicación probados E2E; respuestas por correo con comandos. |
| **Reglas de negocio / automatización** | PARCIAL | Constructor visual de reglas para ticket (ver sección 8). |
| **Portal de autoservicio** | PARCIAL | MyHelpdesk existe; falta catálogo de servicios con formularios dinámicos y aprobaciones. |
| **Gestión de problemas (Problem)** | PENDIENTE | No hay entidad Problem; agrupar incidentes recurrentes en una causa raíz. |
| **Gestión de cambios (Change)** | PENDIENTE | Solicitudes de cambio con aprobación, ventana e implementación (puede disparar despliegues de software). |
| **Base de conocimiento** | PENDIENTE | Artículos, sugerencias automáticas al crear ticket y al usar el asistente. |
| **CMDB / activos** | PARCIAL | Los dispositivos MDM son la base; relacionar usuarios, software, contratos y garantías. |
| **Encuestas CSAT / KPIs** | PARCIAL | CSAT al cierre y tableros por agente/sitio. |
| **Integración con dispositivos y remoto** | PARCIAL | Botón "Conectar" desde el ticket que abra sesión remota con auditoría ligada al ticket. |

---

## 7. Escritorio remoto: por qué es lento y por qué no controlas ratón/teclado

**Hallazgo principal:** el flujo completo de un frame es GDI CopyFromScreen $\rightarrow$ redimensionado bicúbico $\rightarrow$ JPEG de pantalla completa $\rightarrow$ Base64 $\rightarrow$ JSON por SignalR $\rightarrow$ el hub lo reenvía $\rightarrow$ el navegador lo convierte en data URL dentro del estado de React $\rightarrow$ `<img>`. Cada paso es costoso y todo se hace en serie.

### 7.1 Causas de la lentitud de imagen (todas en el código)

| # | Causa | Dónde | Impacto |
| :--- | :--- | :--- | :--- |
| 1 | Captura con Graphics.CopyFromScreen (GDI). Es CPU, bloquea y no detecta cambios. | `RemoteHost/Capture/DesktopCaptureService.cs` (l. 248) | **CRÍTICO** |
| 2 | Se envía la pantalla completa en cada frame, aunque nada cambió. Sin dirty rectangles ni deduplicación. | `DesktopCaptureService.cs` | **CRÍTICO** |
| 3 | JPEG con calidad fija 40 y redimensionado con interpolación bicúbica en CPU. | `DesktopCaptureService.cs` (l. 196, 311) | **ALTO** |
| 4 | Base64 dentro de JSON: +33% de tamaño y asignación de strings enormes por frame. | `RemoteTransportClient.cs` (l. 452) | **CRÍTICO** |
| 5 | InvokeAsync espera confirmación por cada frame: el siguiente no sale hasta que el servidor responde (latencia x FPS). | `RemoteTransportClient.cs` (l. 455) | **ALTO** |
| 6 | Sin control de congestión: no descarta frames viejos ni adapta calidad/FPS según el ancho de banda. | Transporte + hub | **ALTO** |
| 7 | El visor guarda cada frame como string en estado de React y lo convierte en data: URL, forzando re-render y decodificación en el hilo principal. | `RemotePage.tsx` (l. 103-105) | **ALTO** |
| 8 | Protocolo JSON de SignalR (sin MessagePack). | `Program.cs` / `package.json` | **MEDIO** |

### 7.2 Causas probables de que no controles ratón ni teclado
Esto debo confirmarlo ejecutando la app, pero el código señala estas causas (ordenadas por probabilidad):
* **Dos consultas a base de datos por cada movimiento de ratón.** PointerMove llama a GetHumanSessionAsync (FirstOrDefault con tracking) y luego a RequireControlLeaseAsync (otra consulta). A 60 eventos/s son 120 viajes a SQL por segundo y por usuario: el input llega tarde, se acumula o expira. El visor además no tiene throttle.
* **El lease de control:** si el técnico no lo adquirió, expiró o no se renueva, el hub responde "modo solo lectura" y descarta el input. Revisa que el visor llame a AcquireControl y RenewControl en un timer y que muestre claramente el estado.
* **Permiso AllowMouse / AllowKeyboard en la sesión:** si es falso, el hub retorna silenciosamente sin error (PointerMove hace return).
* **Escritorio de entrada incorrecto:** RemoteInputController usa SendInput pero no encontré OpenInputDesktop / SetThreadDesktop. Si el host corre en otra sesión o desktop (UAC, bloqueo, login), el input va al vacío.
* **UIPI y DPI:** un proceso sin elevación no puede enviar input a ventanas elevadas; y si el host no es DPI-aware, las coordenadas normalizadas se desplazan en pantallas con escala 125-150%.
* **Cursor no capturado:** CopyFromScreen no dibuja el cursor (lo dice el propio comentario); el técnico siente que "no hay ratón" aunque el input funcione.

### 7.3 Plan de arreglo por fases (verificable y medible)

| Fase | Cambio | Resultado esperado |
| :--- | :--- | :--- |
| **R1**<br>*(2-4 días, sin cambiar arquitectura)* | • Cachear sesión y lease en memoria (`ConcurrentDictionary` + invalidación por evento); 0 consultas SQL por movimiento.<br>• Throttle del visor a ~60 Hz con coalescencia (solo el último movimiento).<br>• Eliminar Base64: enviar `byte[]` con MessagePack o canal de streaming; descartar frames si hay uno pendiente (drop-oldest).<br>• Pintar con createImageBitmap + canvas, fuera del estado de React.<br>• Diagnóstico visible: FPS, latencia, kbps, estado de lease. | De ~2-5 FPS a 12-20 FPS estables y input fluido en LAN. |
| **R2**<br>*(1-2 semanas)* | • Captura con DXGI Desktop Duplication (Vortice.Windows) con dirty rects y cursor real.<br>• Calidad y resolución adaptativas por ancho de banda; modo "nítido al detenerse".<br>• Entrada en el desktop correcto (OpenInputDesktop), DPI-aware, teclas especiales (Ctrl+Alt+Del vía servicio), portapapeles. | 25-30 FPS en LAN con bajo CPU; control confiable incluyendo UAC. |
| **R3**<br>*(2-4 semanas)* | • Codificación H.264 por hardware (Media Foundation) y reproducción con WebCodecs; o WebRTC (video + data channel de baja latencia, P2P en LAN y TURN opcional).<br>• Transferencia de archivos, chat, grabación y consentimiento del usuario final. | Calidad y latencia comparables a las herramientas comerciales. |

*Recomendación:* empieza por R1. Es el cambio con mejor relación esfuerzo/resultado y te permite medir antes y después. Después decides si R3 se construye o se integra una librería WebRTC madura.

---

## 8. Mesa de ayuda empresarial y 100% automatizada

Tu meta (funcional a nivel empresarial y totalmente automatizada) es alcanzable porque ya tienes los ingredientes: enrutamiento, SLA, correo, dispositivos, comandos remotos, automatización y asistente. Falta conectarlos en un ciclo cerrado.

### 8.1 Ciclo automático objetivo

| Etapa | Qué ocurre automáticamente | Qué ya existe / qué falta |
| :--- | :--- | :--- |
| **1. Entrada** | Correo, portal, alerta de dispositivo (compliance, disco lleno, agente caído), Teams/WhatsApp opcional $\rightarrow$ ticket sin intervención. | Correo y portal existen. *Falta:* ticket desde alertas del MDM. |
| **2. Clasificación** | IA local (Ollama, como dice tu ADR-004) asigna categoría, prioridad (Impacto x Urgencia), sitio y dispositivo relacionado por usuario/host. | Clasificación existe vía OpenRouter. *Falta:* modelo local y fallback por reglas. |
| **3. Enrutamiento** | Asignación por sitio, especialidad, carga y turno. | Ya logrado (`HelpdeskService.Routing`). |
| **4. Auto-resolución** | Runbook por categoría: ejecutar script en el equipo (limpiar disco, reiniciar servicio, reinstalar app), verificar resultado y cerrar con evidencia. | Comandos y scripts existen. *Falta:* catálogo de runbooks enlazados a categorías y verificación post-ejecución. |
| **5. Sugerencia / KB** | Antes de asignar a un humano, el asistente propone artículos y responde al usuario. | *Falta:* base de conocimiento. |
| **6. SLA y escalamiento** | Temporizadores por prioridad y sitio, pausas, avisos al 75%/90%, escalamiento a supervisor y reasignación. | SLA existe. *Falta:* escalamiento por reglas y motor de timers confiable (`BackgroundService` idempotente). |
| **7. Aprobaciones** | Compras, accesos y cambios con flujo de aprobación (jefe, TI, seguridad). | *Falta.* |
| **8. Cierre y calidad** | Cierre automático tras N días sin respuesta, encuesta CSAT, reapertura, reporte por agente/sitio. | Cierre y seguimiento existen; *falta* CSAT y reglas de auto-cierre configurables. |
| **9. Mejora continua** | Detección de incidentes recurrentes $\rightarrow$ Problem; propuesta de nuevo runbook o artículo. | *Falta.* |

### 8.2 Piezas a construir (en este orden)
* **Business Rules Engine de tickets:** Trigger (creado, actualizado, SLA, alerta) $\rightarrow$ Condiciones $\rightarrow$ Acciones (asignar, notificar, ejecutar runbook, cambiar estado, crear tarea). Reutiliza AutomationService con protección anti-loop, dry-run y límite de ejecuciones.
* **Catálogo de runbooks** versionados, firmados y con parámetros; ejecución en agente con verificación de resultado y registro en el ticket.
* **Ticket Device 360:** desde el ticket ver hardware, últimos comandos, línea de tiempo, seguridad y botón "Conectar remoto" con auditoría ligada al ticket.
* **Base de conocimiento + asistente:** el asistente responde con artículos y solo crea ticket si no resuelve.
* Approvals, Problem y Change (módulos livianos primero).
* Pruebas E2E del ciclo (correo $\rightarrow$ ticket $\rightarrow$ asignación $\rightarrow$ remoto $\rightarrow$ cierre $\rightarrow$ reapertura) en Playwright, exigido por tu propia Fase 11.

*Privacidad:* con OpenRouter "free" los textos de tickets (usuarios, equipos, incidentes) salen de tu red. Tu ADR-004 ya decide Ollama local; alinea el código con el ADR y deja OpenRouter solo como opción desactivada por defecto.

---

## 9. Ajustes para que todo funcione como una sola aplicación

Hoy el portal se siente como módulos que conviven; ManageEngine se siente como un producto porque comparte identidad, navegación, datos, estilos y estados. Estos son los ajustes, recorriendo la app de principio a fin.

### 9.1 Login y sesión
* Un solo flujo de autenticación (Entra + local) que emite el token usado por todos los módulos, incluido Ponches. Elimina `app_users.json` y el login propio del servicio Python; el bridge valida el JWT de Titan.
* Manejo de sesión: refresco silencioso, expiración con aviso, cierre de sesión en todas las pestañas y revocación.
* Rate limiting, bloqueo configurable y mensajes de error que no revelan si el usuario existe.
* Carga rápida: el robot de login no debe bloquear la entrada (cargar CSS y animación en diferido, hay 2.253 líneas de CSS).

### 9.2 Transición login $\rightarrow$ portal
* Continuidad visual: el robot del login vuela y se convierte en el asistente del portal (misma figura, mismo estado), usando FLIP/View Transitions. Hoy son dos implementaciones distintas (CSS "mech" y SVG).
* Página de aterrizaje por rol: técnico $\rightarrow$ cola de tickets y alertas; administrador $\rightarrow$ dashboard; usuario final $\rightarrow$ portal MyHelpdesk.

### 9.3 Shell y navegación
* Una sola barra con búsqueda global (`Ctrl+K`) que encuentre dispositivos, usuarios, tickets, grupos, apps y sedes; acciones rápidas desde la paleta.
* Selector de sitio global persistente: filtra dashboards, listas y reportes de todos los módulos.
* Centro de notificaciones único (SignalR) para alertas, SLA, comandos terminados y mensajes del asistente.
* Breadcrumbs y enlaces profundos entre módulos (ticket $\leftrightarrow$ dispositivo $\leftrightarrow$ usuario $\leftrightarrow$ remoto $\leftrightarrow$ auditoría).
* Carga diferida por ruta (solo Ponches usa lazy) y Error Boundaries por módulo.

### 9.4 Datos compartidos (Device 360 / User 360)
* Una sola entidad Dispositivo y una sola entidad Persona (Entra) consumidas por MDM, Helpdesk, Ponches y Reportes.
* Línea de tiempo unificada: auditoría, políticas, apps, seguridad, remoto y tickets.
* Identificadores y estados comunes (Site, Department, Group) en todos los filtros.

### 9.5 Frontend: de "páginas" a producto
* Design System real: tokens + componentes (Table, Drawer, Modal, FormField, Tabs, Toast, EmptyState, ConfirmDialog, Skeleton). Hoy hay 7 componentes y 7 CSS globales (`titan-modules-a/b/c/d`).
* Cliente de API generado desde OpenAPI (`orval`/`openapi-typescript`) en lugar de 23 archivos manuales; tipos compartidos y un manejo de errores único.
* React Query en todo (cache, reintentos, invalidación, paginación server-side) para lograr esa sensación fluida de ManageEngine: listas instantáneas, actualización optimista, sin parpadeos.
* Virtualización de tablas (`TanStack Table` + `Virtual`) para miles de equipos; filtros guardados y columnas configurables.
* Dividir páginas de 2.000+ líneas en hook + secciones; límite de 400 líneas por archivo.

### 9.6 Ponches como módulo nativo
* Retirar el frontend duplicado de `integrations/ponches/frontend` (62 archivos): una sola UI en el portal.
* Python queda como servicio interno detrás de la API de Titan (el navegador nunca lo llama directo), con health real de BioTime y relojes.
* Colaboradores de ponches = usuarios/empleados de Titan (misma fuente); permisos por sitio.
* Cuando exista el Edge Connector, la sincronización con relojes por sede pasa por él.

### 9.7 Tiempo real y agentes
* Separar el hub de notificaciones del canal de alto volumen del escritorio remoto (otro endpoint, otro protocolo).
* Estado de dispositivo único (online/offline, último latido, comando en curso) con la misma lógica en dashboard, lista, ticket y remoto.
* Cola de comandos con ack, reintentos y resultado visible en todas partes.

### 9.8 Operación y plataforma
* Configuración segura por defecto: HTTPS obligatorio, errores detallados solo en Development, IP/URL fuera del código, CORS por ambiente.
* Health checks (`/live`, `/ready`), logs estructurados con `CorrelationId` y OpenTelemetry; panel de salud en el admin.
* Backups SQL automatizados con prueba de restauración; instalador con rollback; firma de agentes e instaladores.
* Pruebas: integración API $\leftrightarrow$ SQL, Playwright de los 5 flujos críticos, pruebas de arquitectura en CI.

---

## 10. Robot del login y agentes IA: movimiento natural

### 10.1 Qué existe hoy
* **Robot de login:** estructura DOM compleja (casco, visor, ojos, pupilas, antena, jetpack, escáner, mandíbula...) animada con ~12 `@keyframes` y 2.253 líneas de CSS; sigue la mirada con una variable `--eye-shift` y respeta `prefers-reduced-motion`.
* **Asistente:** `TitanMovementEngine` (692 líneas) con velocidad, fricción, `requestAnimationFrame` y estados; `TitanBehaviorEngine`, registro de anclas (`AnchorRegistry`), mensajes contextuales, avatar SVG y burbuja de diálogo.
* El diseño ya está orientado a "personaje con física". Lo que probablemente produce saltos es la mezcla de CSS, JS y cambios de ancla: conviene unificarlo en un solo sistema.

### 10.2 Causas típicas de saltos y movimientos bruscos (revisar en el código)
* Cambiar el ancla/objetivo reinicia la trayectoria en vez de redirigirla conservando velocidad.
* Posicionar con `left`/`top` (layout) en vez de `transform: translate3d` (compositor).
* Cambios de estado (hablar, pensar, error) que reemplazan poses de golpe sin fundido entre animaciones.
* Anclas que se recalculan al hacer scroll, redimensionar o navegar y causan teleports.
* Re-render de React por frame o CSS transition compitiendo con la animación JS.

### 10.3 Arquitectura de movimiento recomendada

| Capa | Técnica | Efecto |
| :--- | :--- | :--- |
| **Trayectoria** | Resorte críticamente amortiguado (spring) sobre posición, rotación y escala. Siempre retarget (nunca teleport) conservando velocidad. Límite de aceleración y de "jerk". Curvas Bézier en arco, no líneas rectas. | Cero saltos; arranque y frenado suaves. |
| **Anclas** | Filtro paso-bajo del objetivo + ResizeObserver; al cambiar de ruta el robot espera a que el elemento exista y luego viaja. Evita zonas ocupadas (formularios, botones). | No cubre contenido ni salta al cambiar de pantalla. |
| **Anticipación y seguimiento** | Antes de moverse se inclina hacia el destino (anticipation); al frenar hay pequeño sobreimpulso (follow-through); antena y brazos con resorte secundario. | Se siente vivo y con peso. |
| **Mirada y cabeza** | Orden humano: ojos (0 ms) $\rightarrow$ cabeza (60-120 ms) $\rightarrow$ cuerpo (150-250 ms), con zona muerta y límite de giro. Micro-sacadas de ojo y parpadeo aleatorio (2-6 s, a veces doble). | Mirada creíble. |
| **Respiración / idle** | Ruido suave (Perlin/senoidal) en torso, hombros y flotación; pequeños gestos cada 8-15 s (mirar alrededor, ajustar visor). | Nunca está congelado. |
| **Máquina de estados** | `idle` $\rightarrow$ `atento` $\rightarrow$ `escribiendo` $\rightarrow$ `pensando` $\rightarrow$ `hablando` $\rightarrow$ `éxito` $\rightarrow$ `error` $\rightarrow$ `durmiendo`. Transiciones con mezcla de 150-300 ms, cola con prioridad, cooldowns e interrupción. | Sin cortes entre poses. |
| **Habla / Diálogo** | Boca de 3-4 formas sincronizada con el texto (typewriter) o con la amplitud si hay voz (Web Speech/AudioAnalyser). Tiempo de lectura según palabras; la burbuja aparece con resorte y cola apuntando al robot. | Habla natural. |
| **Rendimiento** | Un solo bucle rAF, escritura de transformaciones con refs/variables CSS (sin re-render), pausa en pestaña oculta, 30 fps en reposo, capa propia (`will-change: transform`), respeta reduced-motion. | 60 fps estables sin consumir CPU. |

### 10.4 Ideas concretas para el login
* **Usuario:** al enfocar el campo, la cabeza se inclina y los ojos leen el texto que escribes (la pupila avanza con el cursor del input).
* **Contraseña:** se cubre el visor con un movimiento suave (no instantáneo); al mostrar contraseña "asoma" un ojo con una pausa pequeña.
* **Error de credenciales:** negación de cabeza con amortiguación (2-3 oscilaciones decrecientes), nunca un shake de CSS brusco.
* **Cargando:** pose "pensando" con escáner; si tarda más de 3 s, comenta *"conectando con el servidor..."*.
* **Éxito:** asentimiento, saludo con el nombre y vuelo continuo hacia la esquina donde vivirá como asistente (transición compartida).
* Sensible al contexto: saludo según hora; si el servidor está caído lo dice; si el caps lock está activo lo señala.
* Seguimiento de cursor solo dentro de un radio; fuera del radio vuelve al centro con retardo natural.

### 10.5 Ideas para el asistente y los agentes en el portal
* **Señalar elementos:** viaja en arco hasta el botón que explica y "apunta" con la mano/antena; luego regresa a su perch sin cruzar contenido importante.
* **Acampar en los bordes:** se asoma desde el borde (peeking) y se esconde cuando el usuario está concentrado (teclado activo).
* **Presencia discreta:** proactividad con cooldowns y preferencia del usuario (ya existe `TitanPreferencesPanel`).
* Chat con streaming token a token, indicador de "pensando", tarjetas de herramienta (*"voy a reiniciar el servicio en PC-045"*) y confirmación antes de acciones sensibles, con auditoría como lo exige tu Fase 15.
* Varios agentes (soporte, seguridad, ponches) con personalidad visual propia pero misma rig de movimiento: cambia color, accesorios y voz, no el motor.
* Sonido opcional muy sutil (clics, "beep" de atención) con interruptor.
* **Accesibilidad:** reduced-motion, modo estático, contraste, lector de pantalla con texto alternativo y botón para silenciar al asistente.

### 10.6 Tecnología: tres caminos

| Opción | Pros | Contras | Cuándo elegirla |
| :--- | :--- | :--- | :--- |
| **Mantener SVG/DOM + motor propio de resortes** | Sin dependencias, control total, ya lo tienes. | Más trabajo de rig y mezcla de poses. | Si quieres terminar rápido y pulir el engine actual. |
| **Rive (2D con máquinas de estado)** | Estados, mezcla, inputs (mirada, hablar) nativos; archivos livianos; excelente para un robot expresivo. | Requiere rediseñar el personaje en el editor. | Recomendada si buscas apariencia de asistente moderno. |
| **Three.js / React Three Fiber (3D)** | Rotación y profundidad reales, iluminación. | Pesado, más complejo, mayor consumo en equipos modestos. | Solo si quieres un avatar 3D completo. |

*Ruta sugerida:* (1) unificar login y asistente en un único motor de movimiento con resortes y retarget; (2) agregar capas procedurales (parpadeo, respiración, mirada); (3) valorar Rive para el personaje final. Incluye una página de pruebas (tipo Storybook) con sliders para afinar rigidez, amortiguación y tiempos sin recompilar.

---

## 11. Debilidades priorizadas

| # | Debilidad | Severidad | Acción |
| :---: | :--- | :--- | :--- |
| **1** | Escritorio remoto lento y sin control confiable | **CRÍTICO** | Plan R1–R3 (sección 7). |
| **2** | Config insegura por defecto (HTTP, errores detallados, AllowedHosts, IP hardcodeada) | **CRÍTICO** | Un appsettings por ambiente + validación de arranque. |
| **3** | Sin rate limiting ni health checks | **CRÍTICO** | `AddRateLimiter`, `MapHealthChecks` (`/live`, `/ready`). |
| **4** | IA en la nube (OpenRouter free) contradice ADR-004 local | **ALTO** | Ollama local por defecto; OpenRouter desactivado. |
| **5** | Controladores y hub acoplados a EF | **ALTO** | Casos de uso + interfaces de consulta, por módulo. |
| **6** | Archivos monolíticos (2.000-3.200 líneas) | **ALTO** | Partir en hooks y secciones; límite 400 líneas. |
| **7** | Sin Edge Connector (9 sedes) | **ALTO** | Diseñar F13 antes de abrir la segunda sede. |
| **8** | Pruebas insuficientes (frontend 0, integración 0, E2E 0) | **ALTO** | Playwright en 5 flujos críticos + integración $\text{API} \leftrightarrow \text{SQL}$. |
| **9** | Sin patch management ni ventanas de mantenimiento | **ALTO** | Módulo de parches con rings. |
| **10**| Sin backups/DR ni code signing | **ALTO** | Scripts SQL + restore test; pipeline de firma. |
| **11**| Ponches duplicado y con usuarios JSON | **MEDIO** | $\text{UI nativa} + \text{SSO Titan} + \text{bridge interno}$. |
| **12**| Helpdesk sin Problem/Change/KB/CSAT | **MEDIO** | Módulos livianos tras la automatización. |
| **13**| CSS global por módulos (a/b/c/d) y pocos componentes base | **MEDIO** | Design System real con tokens. |
| **14**| Program.cs de 648 líneas, partial como parche de tamaño | **MEDIO** | Extensiones por módulo y servicios cohesivos. |
| **15**| Sin observabilidad (logs estructurados/métricas/alertas) | **MEDIO** | Serilog + OpenTelemetry + dashboards. |

---

## 12. Hoja de ruta por fases

Pensada para tu forma de trabajar: una fase a la vez. Yo genero el código, tú compilas/pruebas y me devuelves errores, y avanzamos. Cada fase tiene un criterio de salida medible.

| Fase | Contenido | Criterio de salida | Estimado |
| :--- | :--- | :--- | :--- |
| **A. Escritorio remoto R1** | Cache de lease/sesión, throttle, binario sin Base64, canvas, métricas en pantalla. | $\ge15\text{ FPS}$ en LAN, ratón y teclado responden $<100\text{ ms}$. | 1 semana |
| **B. Base segura y operable** | Config por ambiente, HTTPS, rate limiting, health checks, logging estructurado, IA local por defecto. | Servidor arranca seguro y reporta salud. | 1 semana |
| **C. Arquitectura (módulo piloto)** | Quitar DbContext de controladores de Remote y Helpdesk; casos de uso; pruebas de arquitectura. | 0 controladores nuevos con DbContext; test de arquitectura en CI. | 2 semanas |
| **D. Helpdesk automatizado** | Reglas de negocio, runbooks, ticket desde alertas, escalamiento, CSAT, auto-cierre. | E2E correo $\rightarrow$ ticket $\rightarrow$ runbook $\rightarrow$ cierre pasa con auditoría. | 3-4 semanas |
| **E. Windows nivel Endpoint Central** | Device 360 + timeline, patch management, maintenance windows, rings de software. | Parche piloto producción con evidencia. | 4 semanas |
| **F. Una sola app (UX)** | Shell, búsqueda global, selector de sitio, notificaciones, Design System, cliente OpenAPI, Ponches nativo. | Un solo login, una navegación, cero estilos aislados. | 4 semanas |
| **G. Robot y asistentes** | Motor unificado de resortes, capas procedurales, transición login asistente, (opcional) Rive. | Sin saltos; 60 fps; reduced-motion OK. | 2 semanas |
| **H. Android Enterprise** | QR enrollment, Managed Google Play, kiosko, lost mode, E2E. | Dispositivo de prueba completa el ciclo completo. | 3 semanas |
| **I. Edge + instaladores + firma** | Edge Connector, instaladores firmados, release manifest, backup/DR. | Una sede pierde WAN y se recupera sin pérdida. | 4 semanas |
| **J. Calidad y piloto** | Pruebas de carga, seguridad ofensiva, staging, piloto TIC y 1 sede. | Criterios firmados antes de las 9 sedes. | 4+ semanas |

---

## 13. Alcance y límites de esta revisión

* Revisé el commit ec5ec02 (clon superficial de la rama main): estructura, métricas, referencias entre proyectos, documentación, CI, y el código de remoto, hub, agente, Helpdesk, automatización, Android, robot y asistente.
* No compilé, ejecuté ni medí la app. Las causas de rendimiento y de entrada remota están deducidas del código y deben validarse con métricas (FPS, latencia, CPU).
* No revisé el historial completo de Git ni leí cada archivo de las 227.000 líneas. Donde dice "no encontrado" significa que mis búsquedas no hallaron evidencia; puede existir con otro nombre.
* Las notas (0-100) son un juicio profesional comparativo, no una métrica oficial. La comparación con ManageEngine se basa en sus capacidades generales conocidas, no en una versión específica.

**Siguiente paso recomendado:** empezar por la Fase A (escritorio remoto R1). Dime y te entrego el código en el orden en que lo compilas: primero el hub (cache de lease/sesión), luego el transporte binario del RemoteHost, y al final el visor en canvas.