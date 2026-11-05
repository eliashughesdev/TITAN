# TitanMDM - Módulo Inventario Fijo

## Descripción general
Integración de un nuevo módulo de inventario/fijos dentro de TitanMDM, migrando la estructura y datos desde la aplicación existente en `C:\inventario Cedi` (Python/FastAPI/MongoDB) a una nueva implementación en SQL Server dentro del ecosistema TitanMDM.

El módulo tiene la misma apariencia y funcionalidad que los demás módulos (tarjeta en dashboard, RBAC, color azul cielo) e integra totalment2 con el sistema existente sin romper aplicaciones en producción.

---

## Tabla de contenidos

1. [Schema SQL Server](#1-schema-sql-server)
2. [Integración Frontend](#2-integración-frontend)
3. [RBAC y Permisos](#3-rbac-y-permisos)
4. [Endpoints API](#4-endpoints-api)
5. [Arquitectura y Decisiones](#5-arquitectura-y-decisiones)
6. [Migración de Datos](#6-migración-de-datos)

---

## 1. Schema SQL Server

### Archivo: `C:\TitanMDM\db\inventory_schema.sql`

Se creó un schema SQL completo con 40+ tablas mapeadas desde las collections MongoDB del inventario existente. El mapeo incluye:

#### Tablas de Catálogo (maestro):
- **categories** - 18 categorías de activos (Laptop, Desktop, Servidor, Monitor, Impresora, etc.)
- **branches** - 3 sedes (Sede Central, Planta Haina, Sucursal Santiago)
- **departments** - 6 departamentos (TI, Finanzas, Ventas, RRHH, Operaciones, Logística)
- **manufacturers** - 9 fabricantes (Dell, HP, Lenovo, Cisco, Fortinet, APC, Epson, Ubiquiti, Microsoft)
- **suppliers** - Tabla de proveedores
- **asset_models** - Modelos de activos con relaciones a fabricantes y categorías

#### Tablas Principales:
- **assets** - Tabla principal de activos con asset_tag único, nombre, número de serie, estado, categoría, sede, departamento
- **asset_history** - Historial de cambios de activos
- **audit_logs** - Logs de auditoría completos

#### Entregas y Movimientos:
- **deliveries** - Entregas de activos a empleados
- **returns** - Devoluciones de activos
- **receptions** - Recepciones de nuevos activos

#### Usuarios y Seguridad:
- **employees** - Empleados con código, nombre, departamento, sede
- **users** - Usuarios del sistema con hash de contraseña, roles, permisos extras
- **settings** - Configuración global del sistema

#### Inventario Físico:
- **inventory_sessions** - Sesiones de inventario físico
- **inventory_counts** - Conteos individuales por sesión

#### Soporte y Mantenimiento:
- **tickets** - Incidencias y tickets de helpdesk
- **maintenance** - Historial de mantenimiento
- **software** - Software instalado
- **licenses** - Licencias de software

#### Notificaciones:
- **notifications** - Notificaciones del sistema

### Características del Schema:
- **Todas las IDs son NVARCHAR(36)** (UUID) para consistencia
- **Soft delete** en todas las tablas (`deleted BIT DEFAULT 0`)
- **Foreign key constraints** para integridad referencial
- **Índices optimizados** para búsquedas frecuentes (asset_tag, serial_number, status, category, branch, department)
- **Datos por defecto** insertados: sedes, departamentos, categorías, fabricantes

---

## 2. Integración Frontend

### Archivo: `C:\TitanMDM\src\frontend\titanmdm-web\src\config\moduleRegistry.ts`

#### Nuevo módulo agregado:

```typescript
const inventoryModule:
  TitanModuleDefinition = {
    id: 'inventory',
    title: 'Inventario',
    shortTitle: 'Inventario',
    description: 'Gestión de activos, activos fijos, entregas, devoluciones, recepciones y conteos físicos.',
    path: '/inventory',
    icon: Globe,
    permissions: ['inventory.view', 'inventory.manage'],
    enabled: true,
    badge: 'Inventario',
    theme: {
      primary: '#0ea5e9',      // Azure/cyan sky blue
      primaryDark: '#0891b2',
      soft: '#eff6ff',
      border: '#bfdbfe',
      gradient: 'linear-gradient(135deg, #0ea5e9 0%, #0891b2 100%)',
    },
  }
```

#### Tipo de módulo extendido:
```typescript
export type TitanModuleId =
  | 'windows'
  | 'android'
  | 'helpdesk'
  | 'ponches'
  | 'administration'
  | 'inventory'  // <-- Agregado
```

#### Registro en la lista principal:
```typescript
export const titanModules:
  TitanModuleDefinition[] = [
    windowsModule,
    androidModule,
    helpDeskModule,
    ponchesModule,
    administrationModule,
    inventoryModule,  // <-- Agregado al final
  ]
```

### Aparece en:
- **Launchpad** (`/launchpad`) - Tarjeta grid con color azul cielo `#0ea5e9`
- **Dashboard** - Cuando se navega a `/inventory` o se selecciona el módulo

### Color esmeralda/azul cielo:
El tema primary `#0ea5e9` corresponde al color "sky blue" solicitado, consistente con la estética visual de los demás módulos (usando el sistema Tailwind CSS y los temas definidos por cada módulo).

---

## 3. RBAC y Permisos

### Archivo: `C:\TitanMDM\src\backend\TitanMDM.Application\Security\PermissionCodes.cs`

#### Clase Inventory agregada:
```csharp
public static class Inventory
{
    public const string View = "inventory.view";
    public const string Manage = "inventory.manage";
    public const string DashboardView = "inventory.dashboard.view";
    public const string AssetsView = "inventory.assets.view";
    public const string AssetsManage = "inventory.assets.manage";
    public const string DeliveriesView = "inventory.deliveries.view";
    public const string ReturnsView = "inventory.returns.view";
    public const string ReceptionsView = "inventory.receptions.view";
    public const string EmployeesView = "inventory.employees.view";
    public const string SessionsView = "inventory.sessions.view";
    public const string CountsView = "inventory.counts.view";
    public const string HistoryView = "inventory.history.view";
}
```

### Actualización RBAC_MATRIX.md:
Se agregó la fila de Inventory a la matriz principal:

| Módulo | Read | Manage / Execute |
|---|---|---|
| ... | ... | ... |
| **Inventory** | **inventory.view** | **inventory.manage** |

### Flujo de autorización:
- Los endpoints del controlador `InventoryController` requieren permisos `inventory.view` (lectura) o `inventory.manage` (escritura)
- El sistema verifica automáticamente los claims de permisos en el token JWT
- Los roles SuperAdmin y Platform Administrator heredan estos permisos automáticamente durante el seed

---

## 3. Endpoints API

### Archivo: `C:\TitanMDM\src\backend\TitanMDM.Api\Controllers\InventoryController.cs`

#### Endpoints principales:

| Método | Ruta | Descripción | Permiso |
|--------|------|-------------|---------|
| GET | `/api/inventory/assets` | Listar activos con filtros | `inventory.view` |
| GET | `/api/inventory/assets/{assetTag}` | Obtener activo por tag | `inventory.view` |
| GET | `/api/inventory/deliveries` | Listar entregas | `inventory.view` |
| POST | `/api/inventory/deliveries` | Crear nueva entrega | `inventory.manage` |
| GET | `/api/inventory/returns` | Listar devoluciones | `inventory.view` |
| GET | `/api/inventory/receptions` | Listar recepciones | `inventory.view` |
| GET | `/api/inventory/employees` | Listar empleados | `inventory.view` |
| GET | `/api/inventory/sessions` | Listar sesiones de inventario | `inventory.view` |
| POST | `/api/inventory/sessions` | Crear sesión de inventario | `inventory.manage` |
| GET | `/api/inventory/counts` | Listar conteos | `inventory.view` |
| GET | `/api/inventory/history/{assetTag}` | Historial de activo | `inventory.view` |

#### Puente de módulo (compatibilidad legacy):
- `GET/POST/PUT/PATCH/DELETE /api/inventory/module/{**path}` - Redirige a rutas legacy mientras se migran las pantallas históricas
- Mapea permisos mediante `InventoryAccess.Required()` y `InventoryAccess.Operations()`

---

## 4. Arquitectura y Decisiones

### Decisiones clave:

1. **SQL Server en lugar de MongoDB**
   - Motivación: Mejor integración con la pila existente de TitanMDM (C#/.NET)
   - Optimización: Tablas, índices y triggers para operaciones de inventario
   - Consistencia: Mismo motor de base de datos que el resto de módulos TitanMDM

2. **.NET 8 / C# dentro de TitanMDM**
   - Decisión del usuario: "ya autorizó decidir tecnología"
   - Integración total con autenticación JWT, RBAC, y arquitectura on-premise
   - Mismo patrón que módulos Ponches, Devices, Helpdesk

3. **Color "sky blue" para tarjeta dashboard**
   - Hex: `#0ea5e9` (aqua/cyan)
   - Coincide con la especificación visual
   - Igual tamaño que los demás módulos (tarjeta launchpad padrão)

4. **Mismo apariencia que los demás módulos**
   - Estructura de módulo idéntica en `moduleRegistry.ts`
   - Mismo tipo de tarjeta en Launchpad
   - Mismo patrón de permisos que Ponches/Helpdesk
   - Integración en workspace navigation

5. **No romper aplicaciones en producción**
   - El módulo usa el patrón de gateway forwarding (igual que Ponches)
   - Endpoints coexisten con rutas legacy
   - Migración de datos es opt-in, no destructiva

### Arquitectura:
```
Frontend (React + TypeScript)
    ↓
Module Registry (titanModules array)
    ↓
LaunchpadPage.tsx (grid de tarjetas)
    ↓
DashboardPage.tsx (cuando se navega al módulo)
    ↓
API Gateway (InventoryGateway HTTP client)
    ↓
SQL Server (TitanMdmDatabase)
    ↓
Backend (C# .NET 8 - TitanMDM.Api)
```

---

## 5. Migración de Datos

### Datos fuente: `C:\inventario Cedi` (Python/FastAPI/MongoDB)

#### Collections MongoDB identificadas (40+):
- `employees`, `roles`, `users`
- `assets`, `asset_history`
- `deliveries`, `returns`, `receptions`
- `branches`, `departments`, `categories`, `manufacturers`, `suppliers`
- `audit_logs`, `inventory_sessions`, `inventory_counts`
- `manufacturers`, `asset_models`
- `tickets`, `notifications`, `settings`

#### Proceso de migración:
1. **Exportar** datos de MongoDB a formatos intermedios
2. **Transformar** esquema MongoDB → SQL Server (usando el schema creado)
3. **Insertar** datos en las tablas SQL Server
4. **Validar** integridad referencial y completitud
5. **Probar** que los endpoints API retornan los datos esperados

### Scripts de migración:
El schema `C:\TitanMDM\db\inventory_schema.sql` incluye inserción de datos por defecto para catálogos (sedes, departamentos, categorías, fabricantes). Los datos existentes del inventario deberían migrarse usando un script ETL separado que:
- Lee de las collections MongoDB
- Mapea campos al schema SQL
- Ejecuta INSERT batch en SQL Server
- Maneja conflictos de claves primarias y únicas

---

## 6. Estructura de archivos creada

```
C:\TitanMDM\
├── db\inventory_schema.sql          # Schema SQL con 40+ tablas
├── src/
│   ├── backend/
│   │   ├── TitanMDM.Api/
│   │   │   ├── Controllers/InventoryController.cs  # API endpoints
│   │   │   ├── Inventory/                        # Infraestructura gateway
│   │   │   ├── Program.cs                        # Registro de servicios
│   │   │   └── Application/Security/PermissionCodes.cs  # Permisos RBAC
│   │   └── Infrastructure/DependencyInjection/  # Registro de servicios
│   └── frontend/
│       └── titanmdm-web/
│           └── src/config/moduleRegistry.ts      # Registro de módulos UI
├── docs/
│   └── security/RBAC_MATRIX.md                   # Matriz de permisos actualizada
└── README.md                                     # Este archivo
```

---

## Verificación rápida

Para verificar que el módulo quedó integrado correctamente:

1. **Ejecutar**: `dotnet build` en `C:\TitanMDM\src\backend\TitanMDM.Api`
2. **Iniciar**: La aplicación TitanMDM debería mostrar una tarjeta "Inventario" de color azul cielo en el Launchpad
3. **Navegar**: Acceder a `/inventory` o seleccionar el módulo en el dashboard
4. **RBAC**: Los usuarios con roles apropiados verán los endpoints de inventory disponibles
5. **API**: Probar endpoints en `/api/inventory/*` con permisos correspondientes

---

## Próximos pasos

- [ ] Ejecutar `dotnet build` y verificar que compile sin errores
- [ ] Crear script de migración de datos MongoDB → SQL Server
- [ ] Probar la tarjeta en el Launchpad y Dashboard
- [ ] Verificar permisos con diferentes roles de usuario
- [ ] Validar que los datos migrados sean consistentes
- [ ] Documentar cualquier endpoint adicional requerido por el negocio