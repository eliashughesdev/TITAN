# TitanMDM Enterprise Security Architecture

## 1. Objetivo

Este documento define la arquitectura de seguridad de TitanMDM Enterprise
para despliegues principalmente On-Premise, manteniendo compatibilidad
futura con escenarios híbridos y Azure.

TitanMDM administra dispositivos corporativos, soporte remoto,
identidad, aplicaciones, políticas, Helpdesk, biometría y automatización.
Por esta razón toda decisión de diseño debe asumir que una vulnerabilidad
puede tener impacto directo sobre endpoints empresariales.

---

## 2. Principios

TitanMDM adopta:

- Zero Trust entre componentes.
- Least Privilege.
- Defense in Depth.
- Secure by Default.
- Deny by Default.
- Secrets fuera del código fuente.
- Autorización siempre en backend.
- Auditoría de acciones sensibles.
- Identidades separadas para humanos, servicios y dispositivos.
- Transport Encryption.
- Fail Secure.
- Rotación de credenciales.
- Validación explícita de límites de confianza.

Estar dentro de la red corporativa no constituye una prueba
de identidad ni autorización.

---

## 3. Trust Boundaries

Los principales límites de confianza son:

1. Navegador del operador.
2. IIS / Reverse Proxy.
3. TitanMDM.Api.
4. SQL Server.
5. Windows Agent.
6. RemoteHost.
7. Android Agent.
8. Android Management API.
9. Titan Edge Connector.
10. Servicio Ponches.
11. Microsoft Entra ID.
12. Ollama.
13. Filesystem local.
14. Servicios Windows.
15. Red corporativa.
16. Internet.

Todo tráfico que cruce un límite debe ser autenticado,
autorizado y validado según corresponda.

---

## 4. Identidad humana

TitanMDM soportará:

### Identidad local

Utilizada para:

- bootstrap inicial;
- contingencia;
- administración controlada.

Las contraseñas se almacenan únicamente mediante hashes seguros.

### Microsoft Entra ID

Será el proveedor corporativo recomendado para usuarios.

La identidad autenticada mediante Entra ID deberá mapearse
a un usuario interno TitanMDM.

Entra ID autentica.

TitanMDM autoriza.

---

## 5. Identidades técnicas

Los siguientes componentes deben usar identidad propia:

- Windows Agent;
- RemoteHost;
- Edge Connector;
- Ponches;
- workers;
- integraciones;
- procesos automatizados.

No deben utilizar cuentas administrativas humanas.

---

## 6. Identidad de dispositivos

Cada dispositivo administrado deberá disponer de identidad
independiente.

La autenticación de dispositivos deberá evolucionar hacia:

- DeviceId;
- credencial única;
- rotación;
- expiración cuando aplique;
- revocación;
- almacenamiento protegido.

Una credencial de dispositivo no debe permitir autenticarse
como operador.

---

## 7. RBAC

Modelo:

User
  -> Role
     -> Permission
        -> Scope / Site

Toda operación sensible debe validar permisos en backend.

La UI puede ocultar controles, pero nunca constituye
la única barrera de seguridad.

---

## 8. Sites y scopes

TitanMDM soportará separación organizacional por Site.

Un operador podrá quedar limitado por:

- Site;
- módulo;
- workspace;
- tipo de dispositivo;
- función;
- grupo.

Una autorización global solo deberá concederse a roles
explícitamente autorizados.

---

## 9. JWT

Los JWT de TitanMDM deben:

- utilizar una SigningKey fuera de Git;
- validar issuer;
- validar audience;
- validar firma;
- validar expiración;
- mantener ClockSkew reducido;
- utilizar tiempos de vida limitados.

Los tokens nunca deben aparecer en:

- logs;
- excepciones;
- auditoría;
- URLs salvo casos controlados como negociación SignalR.

---

## 10. Secret Management

No se permiten secretos productivos dentro de:

- Git;
- appsettings versionados;
- código fuente;
- scripts;
- imágenes Docker;
- paquetes de instalación.

### Development

Permitido:

- .NET User Secrets;
- variables de entorno;
- archivos locales ignorados.

### On-Premise Production

Debe existir una abstracción ISecretProvider.

Implementaciones futuras:

- DPAPI;
- Windows Credential Manager;
- vault corporativo;
- HSM cuando aplique.

### Hybrid / Azure

Compatible con:

- Azure Key Vault;
- Managed Identity.

---

## 11. Data Protection

ASP.NET Core Data Protection deberá utilizar un key ring
persistente.

En producción:

- las claves no deben residir en carpetas temporales;
- deben tener ACL restringidas;
- deben respaldarse según política;
- el ApplicationName debe permanecer estable.

---

## 12. HTTPS y TLS

Producción requiere HTTPS.

Debe utilizarse:

- TLS corporativo;
- certificados válidos;
- HSTS;
- Secure Cookies;
- redirección HTTPS;
- Forwarded Headers controlados detrás de IIS.

No se permitirá degradación silenciosa a HTTP en producción.

---

## 13. IIS / Reverse Proxy

IIS funcionará como frontera de entrada On-Premise.

Responsabilidades:

- TLS termination;
- límites de request;
- headers;
- logging operacional;
- protección del proceso;
- permisos de filesystem.

TitanMDM.Api no debe asumir que cualquier header reenviado
es confiable sin una topología conocida.

---

## 14. SignalR

SignalR deberá utilizar autenticación.

Los hubs sensibles deben:

- validar usuario;
- validar dispositivo;
- validar permisos;
- validar sesión;
- limitar acciones.

DetailedErrors deberá permanecer deshabilitado en producción.

---

## 15. Remote Support

Remote Support se considera una superficie de riesgo crítico.

Toda sesión debe requerir:

1. operador autenticado;
2. permiso remote;
3. dispositivo válido;
4. sesión válida;
5. token temporal;
6. registro de participante;
7. lease de control;
8. auditoría.

Debe impedirse:

- hijacking;
- session fixation;
- reuse de tokens;
- acceso cruzado a dispositivos;
- control concurrente no autorizado.

---

## 16. UAC y Secure Desktop

El componente RemoteHost deberá manejar elevación sin
debilitar UAC.

TitanMDM no debe:

- deshabilitar UAC;
- modificar Secure Desktop globalmente;
- almacenar credenciales administrativas en texto plano.

Las interacciones privilegiadas deben quedar auditadas.

---

## 17. Windows Agent

El agente debe:

- ejecutar como servicio controlado;
- validar identidad del backend;
- proteger credenciales locales;
- validar comandos recibidos;
- limitar tipos de comandos;
- soportar revocación;
- mantener logs sin secretos;
- manejar reconnect de forma segura.

Los comandos destructivos deberán contar con autorización
y trazabilidad.

---

## 18. RemoteHost

RemoteHost debe operar con los privilegios mínimos necesarios.

La comunicación con WindowsAgent/TitanMDM debe estar
autenticada.

No debe aceptar conexiones arbitrarias desde la red.

---

## 19. Android Enterprise

Las credenciales de Google Android Management API solo
deben existir del lado servidor.

Nunca deberán incluirse dentro del APK.

El agente Android no debe recibir service-account keys.

---

## 20. Titan Edge Connector

Edge Connector funcionará como frontera entre TitanMDM y
sistemas internos especializados.

Arquitectura objetivo:

TitanMDM
   |
   | HTTPS + autenticación
   v
Edge Connector
   |
   +-- BioTime
   +-- ZKTeco
   +-- servicios internos

Debe evolucionar a mTLS cuando la topología lo requiera.

---

## 21. Ponches

Ponches procesa datos laborales y biométricos sensibles.

Debe aplicar:

- autenticación;
- permisos mínimos;
- cifrado de transporte;
- separación de secrets;
- auditoría;
- retención definida.

TitanMDM no debe exponer la base biométrica directamente
al frontend.

---

## 22. Ollama

Ollama será tratado como motor de inferencia, no como
autoridad.

El LLM nunca decide autorización.

Arquitectura:

User
  |
Titan AI Orchestrator
  |
  +-- Authentication
  +-- Authorization
  +-- Scope
  +-- Context filtering
  +-- Tool registry
  +-- Confirmation
  +-- Execution
  +-- Audit
  |
Ollama

El modelo no tendrá acceso directo a:

- SQL Server;
- PowerShell;
- filesystem sensible;
- credenciales;
- servicios Windows;
- dispositivos.

Toda acción ocurre mediante herramientas controladas.

---

## 23. Prompt Injection

Contenido procedente de:

- tickets;
- correos;
- dispositivos;
- aplicaciones;
- logs;
- documentación;
- páginas externas;

debe considerarse datos no confiables.

Nunca deberá interpretarse automáticamente como instrucción
administrativa.

---

## 24. SQL Server

SQL Server deberá operar usando una cuenta técnica dedicada.

Principios:

- mínimo privilegio;
- acceso restringido por red;
- encryption cuando aplique;
- backups protegidos;
- migrations controladas;
- no utilizar sa desde TitanMDM;
- credenciales fuera de Git.

---

## 25. Migrations

La fuente de verdad del esquema es EF Core Migrations.

No usar:

- EnsureCreated;
- scripts manuales no versionados como mecanismo principal.

Las migrations deben:

- estar versionadas;
- compilar;
- poder actualizar una BD existente;
- poder inicializar una BD vacía.

---

## 26. Bootstrap

Primera instalación:

1. comprobar conectividad SQL;
2. aplicar migrations;
3. crear organización base;
4. crear permisos base;
5. crear SuperAdmin;
6. asignar permisos;
7. registrar finalización.

El bootstrap debe ser idempotente.

Nunca deberá resetear contraseñas existentes.

---

## 27. Logging

Nunca registrar:

- Password;
- PasswordHash;
- JWT;
- RefreshToken;
- SigningKey;
- ClientSecret;
- DeviceSecret;
- API keys;
- private keys;
- connection strings.

---

## 28. Auditoría

Las acciones sensibles deben capturar:

- Actor;
- Action;
- Resource;
- Result;
- Timestamp UTC;
- Site cuando aplique;
- CorrelationId;
- origen.

La auditoría no debe almacenar secretos.

---

## 29. Supply Chain

TitanMDM utiliza:

- GitHub Actions;
- CodeQL;
- Dependabot;
- dependency scanning;
- SBOM;
- hashes;
- artifact policy.

Antes de producción los binarios deberán estar firmados.

---

## 30. Backups

Deben existir backups separados para:

- SQL Server;
- Data Protection keys;
- configuración;
- claves operacionales recuperables.

Todo procedimiento de backup deberá disponer de prueba de restore.

---

## 31. Incident Response

TitanMDM deberá soportar:

- revocar usuarios;
- revocar dispositivos;
- revocar tokens;
- rotar secretos;
- cerrar sesiones remotas;
- deshabilitar agentes;
- conservar evidencia;
- consultar auditoría.

---

## 32. Security Gates

No se permitirá release productivo sin:

- CI verde;
- SAST;
- dependency scan;
- secret scan;
- RBAC tests;
- scope tests;
- remote-support security tests;
- agent-authentication tests;
- TLS validation;
- backup/restore test;
- vulnerability review;
- penetration testing;
- aprobación del piloto.

---

## 33. Revisión

Este documento debe actualizarse cuando cambien:

- autenticación;
- autorización;
- Remote Support;
- agentes;
- Edge Connector;
- IA;
- Android Enterprise;
- arquitectura de despliegue;
- secret management.