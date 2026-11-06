-- =============================================================================
-- TITANMDM - MÓDULO INVENTARIO FIJO
-- Schema SQL Server para integración con C:\inventario Cedi
-- =============================================================================
-- Mapeo de colecciones MongoDB a tablas SQL Server
-- Todos los IDs son NVARCHAR(36) (UUID) para consistencia
-- Soft delete en todas las tablas (deleted BIT DEFAULT 0)
-- =============================================================================

-- ===============================================================
-- 1. TABLAS CATÁLOGO (referencia maestra)
-- ===============================================================

CREATE TABLE categories (
    id NVARCHAR(36) PRIMARY KEY,
    name NVARCHAR(160) NOT NULL,
    type NVARCHAR(60) NOT NULL,
    depreciation_years INT NOT NULL,
    deleted BIT DEFAULT 0,
    deleted_at NVARCHAR(40),
    created_at NVARCHAR(40) NOT NULL,
    updated_at NVARCHAR(40),
    CONSTRAINT UC_categories_name UNIQUE (name)
);

CREATE TABLE branches (
    id NVARCHAR(36) PRIMARY KEY,
    name NVARCHAR(160) NOT NULL,
    code NVARCHAR(40) NOT NULL,
    city NVARCHAR(120) NOT NULL,
    address NVARCHAR(300),
    phone NVARCHAR(40),
    deleted BIT DEFAULT 0,
    deleted_at NVARCHAR(40),
    created_at NVARCHAR(40) NOT NULL,
    updated_at NVARCHAR(40)
);
CREATE INDEX IX_branches_code ON branches(code);

CREATE TABLE departments (
    id NVARCHAR(36) PRIMARY KEY,
    name NVARCHAR(160) NOT NULL,
    code NVARCHAR(40) NOT NULL,
    deleted BIT DEFAULT 0,
    deleted_at NVARCHAR(40),
    created_at NVARCHAR(40) NOT NULL
);
CREATE INDEX IX_departments_code ON departments(code);

CREATE TABLE manufacturers (
    id NVARCHAR(36) PRIMARY KEY,
    name NVARCHAR(160) NOT NULL,
    deleted BIT DEFAULT 0,
    deleted_at NVARCHAR(40),
    created_at NVARCHAR(40) NOT NULL
);

CREATE TABLE suppliers (
    id NVARCHAR(36) PRIMARY KEY,
    name NVARCHAR(200) NOT NULL,
    rnc NVARCHAR(40),
    contact_name NVARCHAR(160),
    phone NVARCHAR(40),
    email NVARCHAR(160),
    address NVARCHAR(300),
    deleted BIT DEFAULT 0,
    deleted_at NVARCHAR(40),
    created_at NVARCHAR(40) NOT NULL,
    updated_at NVARCHAR(40)
);
CREATE INDEX IX_suppliers_rnc ON suppliers(rnc);

CREATE TABLE asset_models (
    id NVARCHAR(36) PRIMARY KEY,
    name NVARCHAR(160) NOT NULL,
    model_number NVARCHAR(120),
    manufacturer_id NVARCHAR(36),
    category_id NVARCHAR(36),
    deleted BIT DEFAULT 0,
    deleted_at NVARCHAR(40),
    created_at NVARCHAR(40) NOT NULL,
    updated_at NVARCHAR(40) NOT NULL,
    CONSTRAINT FK_model_man FOREIGN KEY (manufacturer_id) REFERENCES manufacturers(id),
    CONSTRAINT FK_model_cat FOREIGN KEY (category_id) REFERENCES categories(id)
);

-- ===============================================================
-- 2. TABLAS PRINCIPLES - ACTIVOS
-- ===============================================================

CREATE TABLE assets (
    id NVARCHAR(36) PRIMARY KEY,
    asset_tag NVARCHAR(60) UNIQUE NOT NULL,
    name NVARCHAR(200) NOT NULL,
    serial_number NVARCHAR(120),
    category_id NVARCHAR(36),
    manufacturer_id NVARCHAR(36),
    model_id NVARCHAR(36),
    branch_id NVARCHAR(36),
    department_id NVARCHAR(36),
    location_id NVARCHAR(36),
    supplier_id NVARCHAR(36),
    status NVARCHAR(40) NOT NULL DEFAULT 'disponible',
    condition NVARCHAR(40) DEFAULT 'nuevo',
    purchase_date NVARCHAR(40),
    purchase_cost DECIMAL(18,2),
    warranty_end NVARCHAR(40),
    depreciation_years INT,
    mac_address NVARCHAR(60),
    ip_address NVARCHAR(60),
    hostname NVARCHAR(120),
    specs NVARCHAR('MAX'),
    notes NVARCHAR('MAX'),
    photos NVARCHAR('MAX'),
    documents NVARCHAR('MAX'),
    assigned_to NVARCHAR(160),
    assigned_to_name NVARCHAR(160),
    is_deleted BIT DEFAULT 0,
    deleted BIT DEFAULT 0 DEFAULT 0,
    deleted_at NVARCHAR(40),
    created_at NVARCHAR(40) NOT NULL,
    created_by NVARCHAR(160),
    updated_at NVARCHAR(40),
    updated_by NVARCHAR(160),
    CONSTRAINT FK_asset_cat FOREIGN KEY (category_id) REFERENCES categories(id),
    CONSTRAINT FK_asset_man FOREIGN KEY (manufacturer_id) REFERENCES manufacturers(id),
    CONSTRAINT FK_asset_model FOREIGN KEY (model_id) REFERENCES asset_models(id),
    CONSTRAINT FK_asset_branch FOREIGN KEY (branch_id) REFERENCES branches(id),
    CONSTRAINT FK_asset_dept FOREIGN KEY (department_id) REFERENCES departments(id)
);

-- Índices optimización búsquedas
CREATE INDEX IX_assets_asset_tag ON assets(asset_tag);
CREATE INDEX IX_assets_serial ON assets(serial_number);
CREATE INDEX IX_assets_status ON assets(status);
CREATE INDEX IX_assets_category ON assets(category_id);
CREATE INDEX IX_assets_branch ON assets(branch_id);
CREATE INDEX IX_assets_dept ON assets(department_id);
CREATE INDEX IX_assets_status_del ON assets(status, deleted);
CREATE INDEX IX_assets_tag_del ON assets(asset_tag) INCLUDE (name, status, category_id);

-- ===============================================================
-- 3. HISTORIAL Y AUDITORÍA
-- ===============================================================

CREATE TABLE asset_history (
    id NVARCHAR(36) PRIMARY KEY,
    asset_id NVARCHAR(36) NOT NULL,
    action NVARCHAR(60) NOT NULL,
    detail NVARCHAR('MAX'),
    before_json NVARCHAR('MAX'),
    after_json NVARCHAR('MAX'),
    user_email NVARCHAR(160),
    user_name NVARCHAR(160),
    timestamp NVARCHAR(40) NOT NULL,
    CONSTRAINT FK_hist_asset FOREIGN KEY (asset_id) REFERENCES assets(id)
);
CREATE INDEX IX_hist_asset ON asset_history(asset_id);
CREATE INDEX IX_hist_timestamp ON asset_history(timestamp);

CREATE TABLE audit_logs (
    id NVARCHAR(36) PRIMARY KEY,
    action NVARCHAR(60) NOT NULL,
    entity_type NVARCHAR(80) NOT NULL,
    entity_id NVARCHAR(36),
    description NVARCHAR('MAX'),
    before_json NVARCHAR('MAX'),
    after_json NVARCHAR('MAX'),
    user_id NVARCHAR(36),
    user_email NVARCHAR(160),
    user_name NVARCHAR(160),
    ip_address NVARCHAR(60),
    hostname NVARCHAR(200),
    timestamp NVARCHAR(40) NOT NULL
);
CREATE INDEX IX_audit_ts ON audit_logs(timestamp);
CREATE INDEX IX_audit_entity ON audit_logs(entity_type, entity_id);

-- ===============================================================
-- 4. ENTREGAS Y ASIGNACIONES
-- ===============================================================

CREATE TABLE deliveries (
    id NVARCHAR(36) PRIMARY KEY,
    asset_id NVARCHAR(36) NOT NULL,
    asset_tag NVARCHAR(60) NOT NULL,
    asset_name NVARCHAR(200) NOT NULL,
    serial_number NVARCHAR(120),
    assigned_to_name NVARCHAR(160),
    employee_code NVARCHAR(60),
    department_id NVARCHAR(36),
    branch_id NVARCHAR(36),
    expected_return NVARCHAR(40),
    condition NVARCHAR(40) DEFAULT 'bueno',
    notes NVARCHAR('MAX'),
    signature NVARCHAR('MAX'),
    accessories NVARCHAR('MAX'),
    delivered_by NVARCHAR(160),
    delivered_by_email NVARCHAR(160),
    delivery_date NVARCHAR(40) NOT NULL,
    returned BIT DEFAULT 0,
    return_date NVARCHAR(40),
    deleted BIT DEFAULT 0 DEFAULT 0,
    deleted_at NVARCHAR(40),
    created_at NVARCHAR(40) NOT NULL,
    created_by NVARCHAR(160),
    updated_at NVARCHAR(40),
    updated_by NVARCHAR(160),
    CONSTRAINT FK_deliv_asset FOREIGN KEY (asset_id) REFERENCES assets(id)
);
CREATE INDEX IX_deliv_asset ON deliveries(asset_id);
CREATE INDEX IX_deliv_emp ON deliveries(employee_code);
CREATE INDEX IX_deliv_status ON deliveries(returned, deleted);
CREATE INDEX IX_deliv_date ON deliveries(delivery_date);

CREATE TABLE returns (
    id NVARCHAR(36) PRIMARY KEY,
    delivery_id NVARCHAR(36) NOT NULL,
    asset_id NVARCHAR(36) NOT NULL,
    asset_tag NVARCHAR(60) NOT NULL,
    asset_name NVARCHAR(200) NOT NULL,
    returned_by NVARCHAR(160),
    employee_code NVARCHAR(60),
    received_by NVARCHAR(160),
    condition NVARCHAR(40),
    notes NVARCHAR('MAX'),
    signature NVARCHAR('MAX'),
    return_date NVARCHAR(40) NOT NULL,
    created_at NVARCHAR(40) NOT NULL,
    CONSTRAINT FK_ret_deliv FOREIGN KEY (delivery_id) REFERENCES deliveries(id),
    CONSTRAINT FK_ret_asset FOREIGN KEY (asset_id) REFERENCES assets(id)
);
CREATE INDEX IX_ret_deliv ON returns(delivery_id);
CREATE INDEX IX_ret_asset ON returns(asset_id);

CREATE TABLE receptions (
    id NVARCHAR(36) PRIMARY KEY,
    asset_id NVARCHAR(36),
    description NVARCHAR(300),
    serial_number NVARCHAR(120),
    category_id NVARCHAR(36),
    delivered_by NVARCHAR(160),
    employee_code NVARCHAR(60),
    condition NVARCHAR(40),
    notes NVARCHAR('MAX'),
    signature NVARCHAR('MAX'),
    received_by NVARCHAR(160),
    received_by_email NVARCHAR(160),
    reception_date NVARCHAR(40) NOT NULL,
    created_at NVARCHAR(40) NOT NULL,
    CONSTRAINT FK_recept_asset FOREIGN KEY (asset_id) REFERENCES assets(id),
    CONSTRAINT FK_recept_cat FOREIGN KEY (category_id) REFERENCES categories(id)
);
CREATE INDEX IX_recept_asset ON receptions(asset_id);
CREATE INDEX IX_recept_cat ON receptions(category_id);

-- ===============================================================
-- 5. EMPLEADOS Y USUARIOS
-- ===============================================================

CREATE TABLE employees (
    id NVARCHAR(36) PRIMARY KEY,
    code NVARCHAR(60) UNIQUE NOT NULL,
    name NVARCHAR(160) NOT NULL,
    department_id NVARCHAR(36),
    supervisor NVARCHAR(160),
    email NVARCHAR(160),
    phone NVARCHAR(40),
    position NVARCHAR(100),
    branch_id NVARCHAR(36),
    is_active BIT DEFAULT 1,
    deleted BIT DEFAULT 0,
    deleted_at NVARCHAR(40),
    created_at NVARCHAR(40) NOT NULL,
    updated_at NVARCHAR(40),
    updated_by NVARCHAR(160),
    CONSTRAINT FK_emp_dept FOREIGN KEY (department_id) REFERENCES departments(id),
    CONSTRAINT FK_emp_branch FOREIGN KEY (branch_id) REFERENCES branches(id)
);
CREATE INDEX IX_emps_code ON employees(code);
CREATE INDEX IX_emps_dept ON employees(department_id);
CREATE INDEX IX_emps_active ON employees(is_active);

CREATE TABLE users (
    id NVARCHAR(36) PRIMARY KEY,
    email NVARCHAR(160) UNIQUE NOT NULL,
    name NVARCHAR(160) NOT NULL,
    password_hash NVARCHAR('MAX') NOT NULL,
    role NVARCHAR(80) NOT NULL,
    department_id NVARCHAR(36),
    branch_id NVARCHAR(36),
    employee_code NVARCHAR(60),
    phone NVARCHAR(40),
    is_active BIT DEFAULT 1,
    extra_permissions NVARCHAR('MAX') DEFAULT '[]',
    last_login NVARCHAR(40),
    deleted BIT DEFAULT 0,
    deleted_at NVARCHAR(40),
    created_at NVARCHAR(40) NOT NULL,
    created_by NVARCHAR(160),
    updated_at NVARCHAR(40),
    updated_by NVARCHAR(160),
    CONSTRAINT FK_user_dept FOREIGN KEY (department_id) REFERENCES departments(id),
    CONSTRAINT FK_user_branch FOREIGN KEY (branch_id) REFERENCES branches(id)
);
CREATE INDEX IX_users_email ON users(email);
CREATE INDEX IX_user_dept ON users(department_id);
CREATE INDEX IX_user_active ON users(is_active);

-- ===============================================================
-- 6. CONFIGURACIÓN Y SETTINGS
-- ===============================================================

CREATE TABLE settings (
    id NVARCHAR(36) PRIMARY KEY,
    company_name NVARCHAR(200),
    app_name NVARCHAR(120),
    currency NVARCHAR(10) DEFAULT 'DOP',
    password_min_length INT DEFAULT 8,
    session_minutes INT DEFAULT 60,
    warranty_alert_days INT DEFAULT 60,
    license_alert_days INT DEFAULT 60,
    updated_at NVARCHAR(40)
);

CREATE TABLE login_attempts (
    identifier NVARCHAR(200) PRIMARY KEY,
    count INT DEFAULT 0,
    last_attempt NVARCHAR(40),
    locked_until NVARCHAR(40)
);

-- ===============================================================
-- 7. MANTENIMIENTO Y SOFTWARE
-- ===============================================================

CREATE TABLE maintenance (
    id NVARCHAR(36) PRIMARY KEY,
    asset_tag NVARCHAR(60),
    type NVARCHAR(40),
    technician NVARCHAR(160),
    scheduled_date NVARCHAR(40),
    completed_date NVARCHAR(40),
    cost DECIMAL(18,2),
    status NVARCHAR(40),
    description NVARCHAR('MAX'),
    deleted BIT DEFAULT 0,
    deleted_at NVARCHAR(40),
    created_at NVARCHAR(40) NOT NULL,
    updated_at NVARCHAR(40)
);

CREATE TABLE software (
    id NVARCHAR(36) PRIMARY KEY,
    name NVARCHAR(200) NOT NULL,
    publisher NVARCHAR(160),
    version NVARCHAR(60),
    type NVARCHAR(60),
    notes NVARCHAR('MAX'),
    deleted BIT DEFAULT 0,
    deleted_at NVARCHAR(40),
    created_at NVARCHAR(40) NOT NULL,
    updated_at NVARCHAR(40)
);

CREATE TABLE licenses (
    id NVARCHAR(36) PRIMARY KEY,
    software_name NVARCHAR(200) NOT NULL,
    license_key NVARCHAR(200),
    type NVARCHAR(60),
    seats INT DEFAULT 1,
    seats_used INT DEFAULT 0,
    purchase_date NVARCHAR(40),
    expiry_date NVARCHAR(40),
    supplier_name NVARCHAR(200),
    deleted BIT DEFAULT 0,
    deleted_at NVARCHAR(40),
    created_at NVARCHAR(40) NOT NULL,
    updated_at NVARCHAR(40)
);

-- ===============================================================
-- 8. INVENTARIO FÍSICO
-- ===============================================================

CREATE TABLE inventory_sessions (
    id NVARCHAR(36) PRIMARY KEY,
    name NVARCHAR(200),
    branch_id NVARCHAR(36),
    department_id NVARCHAR(36),
    notes NVARCHAR('MAX'),
    status NVARCHAR(40) DEFAULT 'abierta',
    counted INT DEFAULT 0,
    report NVARCHAR('MAX'),
    created_by NVARCHAR(160),
    deleted BIT DEFAULT 0,
    created_at NVARCHAR(40) NOT NULL
);
CREATE INDEX IX_sess_branch ON inventory_sessions(branch_id);
CREATE INDEX IX_sess_dept ON inventory_sessions(department_id);

CREATE TABLE inventory_counts (
    id NVARCHAR(36) PRIMARY KEY,
    session_id NVARCHAR(36) NOT NULL,
    asset_tag NVARCHAR(60) NOT NULL,
    asset_id NVARCHAR(36),
    asset_name NVARCHAR(200),
    found BIT DEFAULT 0,
    system_status NVARCHAR(40),
    condition NVARCHAR(40),
    location_note NVARCHAR('MAX'),
    counted_by NVARCHAR(160),
    counted_at NVARCHAR(40),
    CONSTRAINT FK_count_session FOREIGN KEY (session_id) REFERENCES inventory_sessions(id)
);
CREATE INDEX IX_count_session ON inventory_counts(session_id);
CREATE INDEX IX_count_asset ON inventory_counts(asset_tag);

-- ===============================================================
-- 9. TICKETS / INCIDENCIAS (integración Helpdesk)
-- ===============================================================

CREATE TABLE tickets (
    id NVARCHAR(36) PRIMARY KEY,
    ticket_number NVARCHAR(40) NOT NULL,
    title NVARCHAR(300) NOT NULL,
    description NVARCHAR('MAX'),
    type NVARCHAR(40),
    priority NVARCHAR(40),
    status NVARCHAR(40) DEFAULT 'abierto',
    asset_id NVARCHAR(36),
    asset_tag NVARCHAR(60),
    category NVARCHAR(80),
    assigned_to NVARCHAR(160),
    branch_id NVARCHAR(36),
    department_id NVARCHAR(36),
    reported_by NVARCHAR(160),
    reported_by_email NVARCHAR(160),
    resolution NVARCHAR('MAX'),
    comments NVARCHAR('MAX'),
    closed_at NVARCHAR(40),
    deleted BIT DEFAULT 0,
    deleted_at NVARCHAR(40),
    created_at NVARCHAR(40) NOT NULL,
    updated_at NVARCHAR(40)
);
CREATE INDEX IX_tickets_asset ON tickets(asset_id);
CREATE INDEX IX_tickets_status ON tickets(status);
CREATE INDEX IX_tickets_dept ON tickets(department_id);

-- ===============================================================
-- 10. NOTIFICACIONES
-- ===============================================================

CREATE TABLE notifications (
    id NVARCHAR(36) PRIMARY KEY,
    user_id NVARCHAR(36),
    title NVARCHAR(200) NOT NULL,
    message NVARCHAR('MAX'),
    is_read BIT DEFAULT 0,
    created_at NVARCHAR(40) NOT NULL
);

-- ===============================================================
-- 11. INDICES ADICIONALES DE RENDIMIENTO
-- ===============================================================

CREATE INDEX IX_assets_search ON assets(
    asset_tag NVARCHAR(60) COLLATE Latin1_General_CI_AS,
    name NVARCHAR(200) COLLATE Latin1_General_CI_AS,
    serial_number NVARCHAR(120) COLLATE Latin1_General_CI_AS
);

-- ===============================================================
-- 12. INSERTAR DATOS BASE (catálogos iniciales)
-- ===============================================================

-- Sedes por defecto (3 como en el Mongo original)
IF NOT EXISTS(SELECT 1 FROM branches WHERE code IN ('SC-01','PH-02','SS-03'))
BEGIN
    INSERT INTO branches (id, name, code, city, address, created_at) VALUES
        (NEWID(), 'Sede Central', 'SC-01', 'Santo Domingo', 'Av. San Martín 236', GETDATE()),
        (NEWID(), 'Planta Haina', 'PH-02', 'Haina', 'Zona Industrial Haina', GETDATE()),
        (NEWID(), 'Sucursal Santiago', 'SS-03', 'Santiago', 'Av. Estrella Sadhalá', GETDATE());
END

-- Departamentos por defecto (6 como en el Mongo original)
IF NOT EXISTS(SELECT 1 FROM departments WHERE code IN ('TI','FIN','VEN','RRHH','OPS','LOG'))
BEGIN
    INSERT INTO departments (id, name, code, created_at) VALUES
        (NEWID(), 'Tecnología', 'TI', GETDATE()),
        (NEWID(), 'Finanzas', 'FIN', GETDATE()),
        (NEWID(), 'Ventas', 'VEN', GETDATE()),
        (NEWID(), 'Recursos Humanos', 'RRHH', GETDATE()),
        (NEWID(), 'Operaciones', 'OPS', GETDATE()),
        (NEWID(), 'Logística', 'LOG', GETDATE());
END

-- Catégorias por defecto (15 como en el Mongo original)
IF NOT EXISTS(SELECT 1 FROM categories WHERE name IN ('Laptop','Desktop','Servidor','Monitor','Impresora','Escáner','Switch','Router','Firewall','Access Point','UPS','Teléfono IP','Dispositivo Móvil','Lector Biométrico','Hand Held','Licencia Software'))
BEGIN
    INSERT INTO categories (id, name, type, depreciation_years, created_at) VALUES
        (NEWID(), 'Laptop', 'computo', 4, GETDATE()),
        (NEWID(), 'Desktop', 'computo', 4, GETDATE()),
        (NEWID(), 'Servidor', 'computo', 5, GETDATE()),
        (NEWID(), 'Monitor', 'periferico', 5, GETDATE()),
        (NEWID(), 'Impresora', 'periferico', 5, GETDATE()),
        (NEWID(), 'Escáner', 'periferico', 5, GETDATE()),
        (NEWID(), 'Switch', 'red', 7, GETDATE()),
        (NEWID(), 'Router', 'red', 7, GETDATE()),
        (NEWID(), 'Firewall', 'red', 5, GETDATE()),
        (NEWID(), 'Access Point', 'red', 5, GETDATE()),
        (NEWID(), 'UPS', 'energia', 4, GETDATE()),
        (NEWID(), 'Teléfono IP', 'telefonia', 5, GETDATE()),
        (NEWID(), 'Dispositivo Móvil', 'movil', 3, GETDATE()),
        (NEWID(), 'Lector Biométrico', 'seguridad', 5, GETDATE()),
        (NEWID(), 'Hand Held', 'movil', 4, GETDATE()),
        (NEWID(), 'Licencia Software', 'software', 1, GETDATE());
END

-- Fabricantes por defecto
IF NOT EXISTS(SELECT 1 FROM manufacturers WHERE name IN ('Dell','HP','Lenovo','Cisco','Fortinet','APC','Epson','Ubiquiti','Microsoft'))
BEGIN
    INSERT INTO manufacturers (id, name, created_at) VALUES
        (NEWID(), 'Dell', GETDATE()),
        (NEWID(), 'HP', GETDATE()),
        (NEWID(), 'Lenovo', GETDATE()),
        (NEWID(), 'Cisco', GETDATE()),
        (NEWID(), 'Fortinet', GETDATE()),
        (NEWID(), 'APC', GETDATE()),
        (NEWID(), 'Epson', GETDATE()),
        (NEWID(), 'Ubiquiti', GETDATE()),
        (NEWID(), 'Microsoft', GETDATE());
END

-- =============================================================================
-- FIN DEL SCHEMA
-- =============================================================================
-- ============================================================
-- 2026-11-04 · Columnas recuperadas de MongoDB
-- La migracion original descarto estos campos. Idempotente.
-- Las agrega tambien db/sync_inventory_from_mongo.py.
-- ============================================================
IF COL_LENGTH('assets', 'manufacturer') IS NULL ALTER TABLE assets ADD manufacturer NVARCHAR(200) NULL;
IF COL_LENGTH('assets', 'model') IS NULL ALTER TABLE assets ADD model NVARCHAR(200) NULL;
IF COL_LENGTH('assets', 'os_name') IS NULL ALTER TABLE assets ADD os_name NVARCHAR(200) NULL;
IF COL_LENGTH('assets', 'processor') IS NULL ALTER TABLE assets ADD processor NVARCHAR(200) NULL;
IF COL_LENGTH('assets', 'ram_gb') IS NULL ALTER TABLE assets ADD ram_gb FLOAT NULL;
IF COL_LENGTH('assets', 'disk_total_gb') IS NULL ALTER TABLE assets ADD disk_total_gb FLOAT NULL;
IF COL_LENGTH('assets', 'disk_free_gb') IS NULL ALTER TABLE assets ADD disk_free_gb FLOAT NULL;
IF COL_LENGTH('assets', 'last_user') IS NULL ALTER TABLE assets ADD last_user NVARCHAR(200) NULL;
IF COL_LENGTH('assets', 'last_seen') IS NULL ALTER TABLE assets ADD last_seen NVARCHAR(40) NULL;
IF COL_LENGTH('assets', 'last_inventory_at') IS NULL ALTER TABLE assets ADD last_inventory_at NVARCHAR(40) NULL;
IF COL_LENGTH('assets', 'source') IS NULL ALTER TABLE assets ADD source NVARCHAR(60) NULL;
IF COL_LENGTH('assets', 'deleted_by') IS NULL ALTER TABLE assets ADD deleted_by NVARCHAR(160) NULL;
IF COL_LENGTH('deliveries', 'employee_id') IS NULL ALTER TABLE deliveries ADD employee_id NVARCHAR(36) NULL;
IF COL_LENGTH('deliveries', 'mac_address') IS NULL ALTER TABLE deliveries ADD mac_address NVARCHAR(40) NULL;
IF COL_LENGTH('returns', 'employee_id') IS NULL ALTER TABLE returns ADD employee_id NVARCHAR(36) NULL;
IF COL_LENGTH('returns', 'serial_number') IS NULL ALTER TABLE returns ADD serial_number NVARCHAR(120) NULL;
IF COL_LENGTH('returns', 'branch_id') IS NULL ALTER TABLE returns ADD branch_id NVARCHAR(36) NULL;
IF COL_LENGTH('returns', 'department_id') IS NULL ALTER TABLE returns ADD department_id NVARCHAR(36) NULL;

-- ============================================================
-- 2026-11-06 · Entregas con impresora portátil Honeywell RP4
-- La Handheld puede entregarse sola o junto con una RP4 del
-- inventario (printer_id -> assets.id). Idempotente.
-- ============================================================
IF COL_LENGTH('deliveries', 'include_printer') IS NULL ALTER TABLE deliveries ADD include_printer BIT NOT NULL CONSTRAINT DF_deliv_include_printer DEFAULT 0;
IF COL_LENGTH('deliveries', 'printer_id') IS NULL ALTER TABLE deliveries ADD printer_id NVARCHAR(36) NULL;
IF COL_LENGTH('deliveries', 'printer_serial_number') IS NULL ALTER TABLE deliveries ADD printer_serial_number NVARCHAR(120) NULL;
GO
IF OBJECT_ID('FK_deliv_printer', 'F') IS NULL ALTER TABLE deliveries ADD CONSTRAINT FK_deliv_printer FOREIGN KEY (printer_id) REFERENCES assets(id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_deliv_printer' AND object_id = OBJECT_ID('deliveries')) CREATE INDEX IX_deliv_printer ON deliveries(printer_id);
GO
