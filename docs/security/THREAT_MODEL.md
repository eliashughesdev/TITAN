
---

# 4. `docs/security/THREAT_MODEL.md`

```markdown
# TitanMDM Threat Model

## Modelo

Metodología principal: STRIDE.

## Activos críticos

- credenciales;
- tokens;
- secretos de agentes;
- datos de usuarios;
- inventario;
- políticas;
- sesiones remotas;
- tickets;
- datos biométricos;
- logs;
- backups.

## Amenazas principales

### Spoofing

Riesgos:

- usuario falso;
- agente falso;
- Edge falso;
- sesión remota falsificada.

Controles:

- autenticación;
- tokens;
- secrets rotables;
- TLS;
- validación backend.

### Tampering

Riesgos:

- alterar políticas;
- comandos;
- paquetes;
- reportes;
- binaries.

Controles:

- autorización;
- hashes;
- firmas;
- auditoría;
- TLS.

### Repudiation

Riesgo:

un operador niega haber ejecutado una acción.

Control:

auditoría inmutable operacionalmente.

### Information Disclosure

Riesgos:

- secretos;
- PII;
- attendance;
- inventario;
- tickets.

Controles:

- RBAC;
- TLS;
- secret storage;
- data minimization;
- redaction.

### Denial of Service

Superficies:

- login;
- API;
- SignalR;
- Remote Support;
- agentes;
- integrations.

Controles:

- rate limiting;
- timeouts;
- limits;
- queues;
- backpressure;
- health monitoring.

### Elevation of Privilege

Riesgos:

- bypass RBAC;
- abuso de herramientas IA;
- ejecución privilegiada;
- UAC/RemoteHost.

Controles:

- backend authorization;
- explicit permissions;
- scoped actions;
- confirmation;
- audit.

## Riesgos especialmente críticos

### Remote Support

Impacto potencial: crítico.

Debe impedir:

- sesiones anónimas;
- tokens reutilizables indefinidamente;
- acceso a otro dispositivo;
- takeover entre operadores.

### Agent Commands

Impacto potencial: crítico.

Cada command debe validar:

- tipo;
- dispositivo;
- autenticidad;
- estado;
- payload;
- expiración.

### AI Tooling

El modelo nunca decide permisos.

La autorización ocurre fuera del LLM.

### Ponches

Los datos de asistencia deben considerarse información
corporativa sensible.

La integración debe ser autenticada y limitada.

### Android Enterprise

Las credenciales de Google deben permanecer únicamente en
servidores autorizados.

## Revisión

Este threat model debe actualizarse cuando:

- aparezca un nuevo servicio;
- cambie autenticación;
- cambie Remote Support;
- aparezca un nuevo agente;
- se agregue una integración;
- se agreguen herramientas IA críticas.
