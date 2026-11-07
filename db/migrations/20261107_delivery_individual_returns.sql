-- 2026-11-07: Devoluciones individuales Hand Held / RP4D en deliveries (cisa_ti_itam).
-- Idempotente. Reemplaza columnas antiguas de impresora por columnas RP4D.

IF COL_LENGTH('deliveries', 'returned_handheld') IS NULL ALTER TABLE deliveries ADD returned_handheld BIT NOT NULL CONSTRAINT DF_deliv_returned_handheld DEFAULT 0;
IF COL_LENGTH('deliveries', 'handheld_return_date') IS NULL ALTER TABLE deliveries ADD handheld_return_date DATETIME2 NULL;
IF COL_LENGTH('deliveries', 'returned_rp4d') IS NULL ALTER TABLE deliveries ADD returned_rp4d BIT NOT NULL CONSTRAINT DF_deliv_returned_rp4d DEFAULT 0;
IF COL_LENGTH('deliveries', 'rp4d_return_date') IS NULL ALTER TABLE deliveries ADD rp4d_return_date DATETIME2 NULL;
IF COL_LENGTH('deliveries', 'include_rp4d') IS NULL ALTER TABLE deliveries ADD include_rp4d BIT NOT NULL CONSTRAINT DF_deliv_include_rp4d DEFAULT 0;
IF COL_LENGTH('deliveries', 'rp4d_id') IS NULL ALTER TABLE deliveries ADD rp4d_id NVARCHAR(36) NULL;
IF COL_LENGTH('deliveries', 'rp4d_serial_number') IS NULL ALTER TABLE deliveries ADD rp4d_serial_number NVARCHAR(120) NULL;
GO

IF COL_LENGTH('deliveries', 'include_printer') IS NOT NULL
    EXEC('UPDATE deliveries SET include_rp4d = include_printer WHERE include_rp4d = 0 AND include_printer = 1');
IF COL_LENGTH('deliveries', 'printer_id') IS NOT NULL
    EXEC('UPDATE deliveries SET rp4d_id = printer_id WHERE rp4d_id IS NULL AND printer_id IS NOT NULL');
IF COL_LENGTH('deliveries', 'printer_serial_number') IS NOT NULL
    EXEC('UPDATE deliveries SET rp4d_serial_number = printer_serial_number WHERE rp4d_serial_number IS NULL AND printer_serial_number IS NOT NULL');
GO

IF OBJECT_ID('FK_deliv_rp4d', 'F') IS NULL ALTER TABLE deliveries ADD CONSTRAINT FK_deliv_rp4d FOREIGN KEY (rp4d_id) REFERENCES assets(id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_deliv_rp4d' AND object_id = OBJECT_ID('deliveries')) CREATE INDEX IX_deliv_rp4d ON deliveries(rp4d_id);
GO

IF OBJECT_ID('FK_deliv_printer', 'F') IS NOT NULL ALTER TABLE deliveries DROP CONSTRAINT FK_deliv_printer;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_deliv_printer' AND object_id = OBJECT_ID('deliveries')) DROP INDEX IX_deliv_printer ON deliveries;
IF COL_LENGTH('deliveries', 'include_printer') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_deliv_include_printer' AND parent_object_id = OBJECT_ID('deliveries'))
        ALTER TABLE deliveries DROP CONSTRAINT DF_deliv_include_printer;
    ALTER TABLE deliveries DROP COLUMN include_printer;
END
IF COL_LENGTH('deliveries', 'printer_id') IS NOT NULL ALTER TABLE deliveries DROP COLUMN printer_id;
IF COL_LENGTH('deliveries', 'printer_serial_number') IS NOT NULL ALTER TABLE deliveries DROP COLUMN printer_serial_number;
GO
-- Entregas legadas (anteriores a las devoluciones individuales): solo tenían
-- "returned". returned = 1 significa que la entrega se cerró completa, así que
-- Hand Held y RP4D (si estaba incluida) quedan marcadas como devueltas con la
-- fecha de return_date. Las entregas con returned = 0 siguen pendientes: no se
-- inventan devoluciones. Idempotente.
UPDATE deliveries
SET returned_handheld = 1,
    handheld_return_date = COALESCE(handheld_return_date,
        CONVERT(DATETIME2, SWITCHOFFSET(TRY_CONVERT(DATETIMEOFFSET, return_date, 127), '+00:00')))
WHERE returned = 1 AND returned_handheld = 0;
UPDATE deliveries
SET returned_rp4d = 1,
    rp4d_return_date = COALESCE(rp4d_return_date,
        CONVERT(DATETIME2, SWITCHOFFSET(TRY_CONVERT(DATETIMEOFFSET, return_date, 127), '+00:00')))
WHERE returned = 1 AND include_rp4d = 1 AND returned_rp4d = 0;
GO
