# TitanMDM Enterprise
## Product Requirements Document

**Producto:** TitanMDM
**Tipo:** Enterprise Device Management / IT Operations Platform
**Arquitectura prioritaria:** On-Premise
**Compatibilidad futura:** Hybrid / Azure
**Estado:** En desarrollo
**Plataformas principales:** Windows, Android Enterprise
**Servicios adicionales:** Helpdesk, Ponches, Remote Support, Automation, AI

---

# 1. Visión

TitanMDM es una plataforma empresarial para administrar,
proteger, auditar y dar soporte a dispositivos corporativos
desde una única consola.

La solución debe permitir operar principalmente dentro de
infraestructura on-premise sin depender obligatoriamente
de servicios cloud para sus funciones principales.

La arquitectura debe conservar compatibilidad futura con
escenarios híbridos y Azure.

---

# 2. Objetivos

TitanMDM debe proporcionar:

- administración centralizada de dispositivos Windows;
- administración Android Enterprise;
- inventario de hardware y software;
- políticas de configuración;
- cumplimiento y seguridad;
- distribución de aplicaciones;
- soporte remoto;
- administración de agentes;
- Helpdesk integrado;
- gestión de asistencia mediante Ponches;
- automatizaciones;
- auditoría completa;
- reportes;
- RBAC empresarial;
- integración con Microsoft Entra ID;
- asistentes locales mediante Ollama;
- funcionamiento multiusuario;
- capacidad multi-sede;
- observabilidad;
- backup y recuperación;
- instaladores empresariales;
- actualización controlada de componentes.

---

# 3. Principios del producto

## 3.1 Seguridad por diseño

Todas las funciones administrativas deben aplicar:

- autenticación;
- autorización;
- permisos explícitos;
- auditoría;
- validación de entradas;
- protección de secretos;
- transporte seguro;
- principio de mínimo privilegio.

## 3.2 On-Premise First

Las funciones críticas deben poder operar dentro de la
infraestructura corporativa.

La solución no debe requerir Azure para funcionar.

## 3.3 Cloud Ready

Las abstracciones internas deben permitir en el futuro usar:

- Azure Key Vault;
- Azure SQL;
- Azure Storage;
- Azure SignalR;
- App Service;
- Azure Monitor;
- otros servicios equivalentes.

## 3.4 Modularidad

Cada dominio funcional debe mantener límites claros para
reducir acoplamiento.

## 3.5 Auditabilidad

Las acciones administrativas relevantes deben producir
trazas consultables.

## 3.6 Operabilidad

TitanMDM debe permitir:

- diagnóstico;
- health checks;
- logging;
- métricas;
- recuperación;
- actualización;
- mantenimiento.

---

# 4. Usuarios

## 4.1 SuperAdmin

Acceso total al producto.

Responsabilidades:

- configuración global;
- RBAC;
- seguridad;
- integraciones;
- administración de operadores;
- configuración de sitios;
- configuraciones avanzadas.

## 4.2 Administrador TI

Administra dispositivos y módulos permitidos por RBAC.

## 4.3 Técnico Helpdesk

Administra tickets, soporte y dispositivos dentro de su
alcance.

## 4.4 Usuario corporativo

Puede acceder a Helpdesk cuando tenga autorización.

No debe visualizar módulos administrativos.

## 4.5 Servicios y agentes

Los agentes Windows, Android, RemoteHost y Edge Connector
usan identidades técnicas independientes de usuarios humanos.

---

# 5. Workspaces

TitanMDM se organiza en workspaces.

## Windows

Incluye:

- dispositivos;
- grupos;
- inventario;
- aplicaciones;
- políticas;
- cumplimiento;
- seguridad;
- reportes;
- soporte remoto;
- enrolamiento.

## Android

Incluye:

- Android Enterprise;
- dispositivos;
- políticas;
- aplicaciones;
- Kiosk;
- Lost Mode;
- ubicación;
- geofencing;
- cumplimiento.

## Helpdesk

Incluye:

- tickets;
- estados;
- conversaciones;
- adjuntos;
- SLA;
- zonas;
- grupos;
- técnicos;
- asignación automática;
- correo;
- Entra ID;
- asistente IA.

## Ponches

Incluye:

- colaboradores;
- dispositivos biométricos;
- registros;
- horarios;
- inventario;
- sincronización;
- reportes;
- ponche remoto;
- Fiorella.

## Administración

Incluye:

- usuarios;
- roles;
- permisos;
- configuración;
- sitios;
- auditoría;
- integraciones.

---

# 6. Windows Management

TitanMDM debe administrar equipos Windows mediante un
servicio agente corporativo.

Funciones principales:

- enrolamiento;
- heartbeat;
- inventario;
- comandos;
- políticas;
- software;
- seguridad;
- cumplimiento;
- automatización;
- soporte remoto;
- actualizaciones del agente.

El agente debe recuperarse de:

- reinicios;
- pérdida temporal de red;
- caída del backend;
- pérdida temporal de SignalR;
- reinicio del servicio.

---

# 7. Remote Support

Debe permitir sesiones remotas controladas y auditadas.

Requisitos:

- sesiones concurrentes;
- autenticación;
- autorización;
- control de acceso;
- streaming;
- teclado;
- mouse;
- cursor visible;
- selección de monitor;
- soporte multi-monitor;
- reconexión;
- UAC;
- Secure Desktop;
- indicador de sesión;
- auditoría.

Nunca debe existir una sesión remota no autorizada.

---

# 8. Android Enterprise

La plataforma debe soportar Android Enterprise mediante
Google Android Management API.

Debe contemplar:

- enrolamiento;
- políticas;
- aplicaciones;
- comandos;
- sincronización;
- Kiosk;
- compliance;
- ubicación cuando esté autorizada;
- Lost Mode;
- geofencing.

---

# 9. Helpdesk

El Helpdesk debe funcionar como mesa de servicio integrada.

Estados mínimos:

- Abierto;
- En proceso;
- En espera por usuario;
- Resuelto;
- Cerrado;
- Reabierto.

Debe incluir:

- creación manual;
- creación por correo;
- asignación;
- reasignación;
- comentarios;
- notas internas;
- adjuntos;
- prioridad;
- categoría;
- área;
- zona;
- SLA;
- historial;
- búsqueda;
- KPIs;
- dashboards.

---

# 10. Ponches

Ponches se mantendrá como servicio especializado Python.

TitanMDM actuará como plataforma principal.

El servicio Python se utilizará especialmente para:

- integración ZKTeco;
- biometría;
- asistencia;
- sincronización;
- reportes específicos.

No se duplicará código Python innecesariamente.

---

# 11. Identidad y RBAC

TitanMDM debe soportar:

- identidad local;
- Microsoft Entra ID;
- roles;
- permisos granulares;
- scopes;
- restricciones por Site;
- restricciones por módulo.

Toda acción sensible debe validar autorización en backend.

La UI nunca será la única barrera de seguridad.

---

# 12. Sites

TitanMDM debe soportar múltiples sitios corporativos.

Un Site representa una ubicación o entorno operativo.

Los dispositivos, operadores y recursos podrán asociarse a
Sites.

La autorización futura podrá limitar acciones según Site.

---

# 13. Auditoría

La auditoría debe registrar como mínimo:

- usuario o actor;
- acción;
- recurso;
- fecha UTC;
- resultado;
- origen;
- Site cuando aplique;
- información necesaria para trazabilidad.

Debe evitarse almacenar secretos, contraseñas o tokens.

---

# 14. Inteligencia Artificial

TitanMDM integrará Ollama como plataforma principal de IA
local.

Asistentes:

- Titan;
- Fiorella.

La IA puede:

- explicar;
- recomendar;
- resumir;
- buscar contexto;
- asistir operadores;
- invocar herramientas autorizadas.

La IA no debe saltarse RBAC.

Las herramientas con impacto deben:

1. validar permiso;
2. validar scope;
3. solicitar confirmación cuando corresponda;
4. ejecutar;
5. auditar.

---

# 15. Titan y Fiorella

Los asistentes visuales tendrán estados deterministas.

Ejemplos:

- idle;
- thinking;
- speaking;
- working;
- success;
- warning;
- error;
- pointing;
- walking;
- entering;
- exiting.

Titan podrá adicionalmente utilizar:

- flying;
- landing;
- takeoff.

Las animaciones no deben basarse en movimientos aleatorios.

Deben responder a eventos reales de la aplicación.

---

# 16. Requisitos no funcionales

## Disponibilidad

La aplicación debe recuperarse de fallas temporales sin
corromper operaciones.

## Rendimiento

Las operaciones de consulta comunes deben responder de forma
adecuada para uso empresarial.

## Escalabilidad

Debe soportar múltiples operadores y una flota creciente de
dispositivos.

## Seguridad

Debe cumplir prácticas de desarrollo seguro empresarial.

## Observabilidad

Debe existir capacidad para observar:

- logs;
- errores;
- disponibilidad;
- rendimiento;
- jobs;
- agentes.

## Recuperación

Debe existir estrategia de:

- backup;
- restore;
- disaster recovery.

---

# 17. Plataformas

Backend:

- .NET / C#;
- ASP.NET Core;
- Entity Framework Core;
- SQL Server;
- SignalR.

Frontend:

- React;
- TypeScript;
- Vite.

Windows:

- Windows Service;
- .NET;
- RemoteHost.

Android:

- Kotlin;
- Android Enterprise.

Ponches:

- Python;
- FastAPI;
- ZKTeco.

IA:

- Ollama.

Hosting principal:

- IIS;
- Windows Server;
- SQL Server.

---

# 18. Criterios Enterprise

Un módulo no se considera terminado únicamente porque
compile.

Para considerarse Enterprise Ready debe:

- funcionar de extremo a extremo;
- validar permisos;
- manejar errores;
- registrar auditoría cuando aplique;
- evitar secretos hardcoded;
- tener UX operable;
- contar con pruebas apropiadas;
- ser observable;
- soportar recuperación;
- tener documentación suficiente.

---

# 19. Estrategia de entrega

Macro fases:

A. Fundaciones técnicas
B. Identidad / RBAC / Sites
C. Windows funcional
D. Agente / Remote Support
E. Helpdesk
F. Ponches
G. Android Enterprise
H. Automation / Patch / Timeline / AI
I. UI/UX final
J. Installers / Signing / Updater
K. Observability / Backup / DR
L. Tests / Security / Load / Resilience
M. Staging / Pilot / Production

---

# 20. Definition of Done

TitanMDM estará listo para producción cuando:

- las funcionalidades críticas estén operativas;
- no existan secretos activos en Git;
- CI esté estable;
- los tests críticos pasen;
- seguridad haya sido validada;
- los instaladores estén firmados;
- actualización y rollback estén probados;
- backup y restore estén probados;
- monitoreo esté operativo;
- documentación esté actualizada;
- piloto empresarial haya sido aprobado.
