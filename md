# TITANMDM ENTERPRISE — AE-FLEET-01

## Control avanzado de teléfonos corporativos y Titan Launcher

**Repositorio:** `C:\TitanMDM`  
**Rama:** `feat/remote-support-rs-h`

### Objetivo

Convertir el módulo Android existente en una consola avanzada de administración empresarial de dispositivos corporativos Fully Managed.

No crear otro backend ni otro agente. Reutilizar Android Management API, Android Device Policy, el agente Kotlin, Titan Launcher, React, SQL Server y los servicios existentes.

### Bloque A — Titan Launcher Studio

Ampliar el diseñador con:

- Carga de fondos de pantalla corporativos y vista previa.
- Almacenamiento seguro de imágenes, validación de formato y tamaño.
- Logotipo, colores, tipografía, nombre empresarial y mensaje inicial.
- Cuadrícula de aplicaciones personalizable.
- Reordenación drag-and-drop y carpetas.
- Aplicaciones visibles, ocultas y bloqueadas.
- Perfiles visuales por organización, localidad y grupo.
- Versionado, publicación, despliegue y reversión.
- Configuración administrada recibida desde Android Enterprise.
- Caché local de la última configuración válida.
- Estado de sincronización y errores reales.

No confundir el fondo del escritorio propio de Titan Launcher con el wallpaper del sistema Android: implementar cada uno solo cuando la API correspondiente lo permita.

### Bloque B — Seguridad empresarial

Extender las políticas y operaciones existentes con:

- Requisitos de PIN y contraseña.
- Tiempo de bloqueo automático.
- Bloqueo remoto.
- Reinicio remoto en dispositivos compatibles.
- Modo perdido.
- Políticas de instalación de aplicaciones.
- Restricciones de configuración.
- Restricciones de depuración USB.
- Restricciones de instalación desde fuentes desconocidas.
- Políticas de Wi-Fi y VPN cuando estén soportadas.
- Protección de configuraciones corporativas.
- Recuperación administrativa y desenrolamiento autorizado.

No almacenar PIN reales en SQL ni en logs. Para operaciones sensibles, aplicar RBAC, auditoría, confirmación y controles adicionales.

### Bloque C — Aplicaciones y lista blanca

Completar:

- Integración Managed Google Play.
- Aplicaciones públicas y privadas.
- Catálogo corporativo por organización.
- Instalación obligatoria y disponible.
- Bloqueo y retirada de aplicaciones.
- Control de versiones.
- Actualización de aplicaciones.
- Estado real de instalación por teléfono.
- Acciones masivas y asignación por grupo.
- Restricciones de ejecución compatibles.
- Borrado de datos de aplicaciones con confirmación.

No prometer forzar el cierre de aplicaciones de terceros mediante APIs no disponibles. Diferenciar suspensión, bloqueo, desinstalación y borrado de datos.

### Bloque D — Soporte remoto Android

Diseñar una arquitectura de soporte remoto integrada con TitanMDM:

- Solicitud de sesión desde React.
- Identidad del técnico y RBAC.
- Señalización mediante servicios existentes.
- Visualización de pantalla cuando Android permita la captura.
- Consentimiento visible para captura donde sea requerido.
- Diagnóstico de pantalla, aplicaciones y conectividad.
- Historial y auditoría de sesiones.
- Revocación y finalización segura de sesiones.
- Investigación y adaptación de Samsung Knox para capacidades OEM adicionales.

Separar claramente:
1. Visualización remota.
2. Control remoto interactivo.
3. Control desatendido validado por fabricante.

No implementar acceso oculto ni eludir restricciones de Android, MediaProjection o consentimiento.

### Bloque E — Inventario y telemetría

Ampliar con:

- Fabricante, modelo, serie disponible y Android.
- Batería, almacenamiento, memoria y conectividad.
- Estado de seguridad y cumplimiento.
- Aplicaciones y versiones.
- Conectividad y consumo de datos cuando sea accesible.
- Último heartbeat y última sincronización.
- Historial de cambios.
- Información de políticas aplicadas.
- Diagnóstico de errores.
- Identificación del modo de administración.

Utilizar únicamente datos disponibles mediante APIs autorizadas y permisos válidos.

### Bloque F — Consola de flota React

Crear una pantalla avanzada con:

- Resumen de flota.
- Filtros por organización, localidad, grupo y estado.
- Ficha de dispositivo.
- Acciones individuales y masivas.
- Editor de aplicaciones y políticas.
- Control de configuración del Launcher.
- Consola de soporte remoto.
- Cola de operaciones.
- Seguimiento de errores.
- Auditoría y exportación.

Aplicar el sistema de diseño actual, color verde Android, CSS centralizado, diseño responsive y componentes compartidos. Evitar duplicaciones y reglas `!important` innecesarias.

### Seguridad y calidad

- RBAC y aislamiento entre organizaciones y localidades.
- Comprobación de compatibilidad de acciones por versión y modo de administración.
- Confirmación de operaciones destructivas.
- Auditoría con operador, fecha, destino y resultado.
- Secretos fuera del repositorio.
- Migraciones nuevas no destructivas.
- Reintentos e idempotencia.
- Pruebas unitarias, integración, React, Kotlin y dispositivo físico.
- Estados reales, sin datos simulados en producción.

### Entrega

Archivos completos y rutas exactas, builds, pruebas, limitaciones de Android, estado de integración Samsung Knox y guía de validación.

No modificar main. No realizar commit ni push automáticamente. No eliminar funcionalidades existentes.


# TITANMDM — COMPLEMENTO OBLIGATORIO AE-FLEET-01

## BLOQUE G — ANDROID ENTERPRISE MULTIMARCA

**Objetivo:** garantizar que TitanMDM sea una solución de administración empresarial multimarca, no exclusiva para Samsung.

### Fabricantes objetivo

- Samsung — One UI.
- Xiaomi — MIUI / HyperOS.
- Redmi — MIUI / HyperOS.
- Motorola — Android / Hello UI.
- Honor — MagicOS.
- Huawei — EMUI / HarmonyOS, sujeto a compatibilidad.

### G1 — Arquitectura universal

1. Mantener Android Management API y Android Device Policy como base común para dispositivos compatibles.
2. No introducir dependencias obligatorias de Samsung Knox.
3. Implementar detección de fabricante, modelo, Android SDK, firmware y servicios Google disponibles.
4. Crear una matriz de capacidades por dispositivo y modo de administración.
5. Utilizar interfaces y adaptadores para funcionalidades específicas de fabricantes.
6. No crear implementaciones duplicadas del agente Kotlin.
7. Reutilizar Titan Launcher como experiencia corporativa común.
8. Mantener RBAC, auditoría y aislamiento organizacional.

### G2 — Aprovisionamiento

Implementar y verificar los flujos compatibles:

- Fully Managed mediante QR.
- Dedicated/Kiosk.
- Zero-touch cuando esté disponible.
- Samsung Knox Mobile Enrollment como integración opcional futura.
- Otros métodos oficiales soportados.

No afirmar compatibilidad universal sin pruebas.

Los dispositivos Huawei sin Google Mobile Services requieren una ruta alternativa y no deben considerarse compatibles con AMAPI por defecto. Registrar explícitamente esta limitación.

### G3 — Políticas y funciones

Diseñar las siguientes capacidades sin acoplamiento a un fabricante:

- PIN y seguridad.
- Bloqueo remoto.
- Aplicaciones obligatorias.
- Lista blanca de Managed Google Play.
- Restricciones corporativas.
- Configuración de Titan Launcher.
- Inventario y telemetría.
- Localización autorizada y modo perdido.
- Administración de grupos y flotas.
- Operaciones masivas.
- Auditoría y cumplimiento.

Las funcionalidades no soportadas deben aparecer deshabilitadas con explicación técnica, nunca como ejecutadas correctamente.

### G4 — Soporte remoto

Separar tres niveles:

1. Diagnóstico remoto.
2. Visualización de pantalla autorizada.
3. Control remoto interactivo o desatendido cuando exista soporte real del fabricante y de Android.

Samsung Knox será un adaptador opcional. No utilizarlo como requisito del motor común.

No implementar mecanismos para eludir protecciones de Android.

### G5 — Consola React

Agregar:

- Fabricante y modelo.
- Versión de Android.
- Estado de Google Mobile Services.
- Modo de administración.
- Capacidades soportadas.
- Acciones disponibles por equipo.
- Filtros multimarca.
- Diagnóstico de incompatibilidades.

Mantener el diseño verde corporativo de Android y utilizar componentes compartidos.

### G6 — Pruebas

Crear pruebas de compatibilidad por fabricante, versión de Android y modo de administración.

Utilizar emuladores Android 13 y 15 para pruebas iniciales.

Preparar pruebas físicas para Samsung, Xiaomi, Redmi, Motorola, Honor y Huawei conforme se disponga de dispositivos.

No declarar validación física de fabricantes que no hayan sido probados.

## REGLAS ADICIONALES

- Implementar los bloques A–G como una solución integrada.
- Priorizar funcionalidades universales sobre adaptaciones OEM.
- Conservar compatibilidad con el backend y agentes existentes.
- No duplicar servicios, políticas ni modelos.
- No implementar capacidades falsas o simuladas en producción.
- No modificar main.
- No hacer commit ni push automáticamente.
- Ejecutar compilaciones y pruebas al terminar.
- Entregar funcionalidades comprobadas, pendientes y bloqueos externos.

**Criterio de aceptación:** TitanMDM debe poder administrar una flota heterogénea desde la misma consola, aplicando políticas comunes y habilitando capacidades específicas únicamente cuando el dispositivo realmente las soporte.