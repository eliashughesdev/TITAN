using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TitanMDM.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TitanMdmDbContext))]
[Migration("20261007190000_HD_C1_EnterpriseHelpdeskCatalog")]
public sealed class HD_C1_EnterpriseHelpdeskCatalog
    : Migration
{
    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            SET NOCOUNT ON;
            SET XACT_ABORT ON;

            DECLARE @NowUtc datetime2 =
                SYSUTCDATETIME();

            /*
             * =====================================================
             * TITANMDM HELPDESK ENTERPRISE CATALOG
             * =====================================================
             *
             * Objetivos:
             *
             * - reutilizar grupos corporativos ya existentes;
             * - completar sus categorías;
             * - crear únicamente especialidades faltantes;
             * - no borrar técnicos;
             * - no borrar coberturas;
             * - no borrar grupos personalizados;
             * - ser idempotente.
             */

            /* =====================================================
               REDES
               ===================================================== */

            UPDATE dbo.HelpdeskTeams
            SET
                Description =
                    CASE
                        WHEN Description IS NULL
                             OR LTRIM(RTRIM(Description)) = ''
                        THEN
                            N'Gestión de conectividad, Internet, Wi-Fi, LAN, VPN, DNS, firewall y comunicaciones.'
                        ELSE Description
                    END,

                Categories =
                    N'|redes y conectividad|internet|wi-fi|lan|vpn|dns|firewall|telefonía|'
            WHERE
                LOWER(LTRIM(RTRIM(Name))) =
                    N'redes';


            /* =====================================================
               SEGURIDAD
               ===================================================== */

            UPDATE dbo.HelpdeskTeams
            SET
                Description =
                    CASE
                        WHEN Description IS NULL
                             OR LTRIM(RTRIM(Description)) = ''
                        THEN
                            N'Gestión de incidentes de seguridad, antivirus, malware, phishing y cumplimiento.'
                        ELSE Description
                    END,

                Categories =
                    N'|seguridad|antivirus|malware|phishing|acceso sospechoso|cumplimiento|bloqueo de seguridad|'
            WHERE
                LOWER(LTRIM(RTRIM(Name))) =
                    N'seguridad';


            /* =====================================================
               SISTEMAS
               ===================================================== */

            UPDATE dbo.HelpdeskTeams
            SET
                Description =
                    CASE
                        WHEN Description IS NULL
                             OR LTRIM(RTRIM(Description)) = ''
                        THEN
                            N'Soporte de sistemas corporativos, aplicaciones empresariales y Microsoft 365.'
                        ELSE Description
                    END,

                Categories =
                    N'|sistemas|aplicaciones empresariales|erp|facturación|microsoft 365|outlook|teams|onedrive|sharepoint|software|'
            WHERE
                LOWER(LTRIM(RTRIM(Name)))
                IN (
                    N'sistema',
                    N'sistemas'
                );


            /* =====================================================
               DESARROLLO
               ===================================================== */

            UPDATE dbo.HelpdeskTeams
            SET
                Description =
                    CASE
                        WHEN Description IS NULL
                             OR LTRIM(RTRIM(Description)) = ''
                        THEN
                            N'Desarrollo de aplicaciones, APIs, integraciones, errores, cambios y despliegues.'
                        ELSE Description
                    END,

                Categories =
                    N'|desarrollo|bug|cambio|despliegue|api|integración|'
            WHERE
                LOWER(LTRIM(RTRIM(Name))) =
                    N'desarrollo';


            /* =====================================================
               SOPORTE GENERAL
               ===================================================== */

            UPDATE dbo.HelpdeskTeams
            SET
                Description =
                    CASE
                        WHEN Description IS NULL
                             OR LTRIM(RTRIM(Description)) = ''
                        THEN
                            N'Recepción general de incidentes, consultas y solicitudes de servicio.'
                        ELSE Description
                    END,

                Categories =
                    N'|general|solicitud de servicio|consulta|otro|hardware|desktop|laptop|monitor|impresora|escáner|periféricos|',

                IsActive =
                    1
            WHERE
                LOWER(LTRIM(RTRIM(Name))) =
                    N'soporte general';


            /* =====================================================
               ACCESOS E IDENTIDAD
               ===================================================== */

            INSERT INTO dbo.HelpdeskTeams
            (
                Id,
                OrganizationId,
                Name,
                Description,
                Categories,
                IsActive,
                CreatedAtUtc
            )
            SELECT
                NEWID(),
                o.Id,
                N'Accesos e Identidad',
                N'Gestión de cuentas, autenticación, permisos, Microsoft Entra ID y acceso corporativo.',
                N'|accesos y cuentas|contraseña|bloqueo de cuenta|entra id|mfa|permisos|nuevo usuario|',
                1,
                @NowUtc
            FROM dbo.Organizations o
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.HelpdeskTeams t
                WHERE
                    t.OrganizationId = o.Id
                    AND LOWER(
                        LTRIM(
                            RTRIM(
                                t.Name))) =
                        N'accesos e identidad'
            );


            /* =====================================================
               INFRAESTRUCTURA
               ===================================================== */

            INSERT INTO dbo.HelpdeskTeams
            (
                Id,
                OrganizationId,
                Name,
                Description,
                Categories,
                IsActive,
                CreatedAtUtc
            )
            SELECT
                NEWID(),
                o.Id,
                N'Infraestructura y Servidores',
                N'Servidores Windows, IIS, SQL Server, almacenamiento, respaldos y virtualización.',
                N'|infraestructura|servidor|iis|sql server|storage|backup|windows|actualizaciones|virtualización|',
                1,
                @NowUtc
            FROM dbo.Organizations o
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.HelpdeskTeams t
                WHERE
                    t.OrganizationId = o.Id
                    AND LOWER(
                        LTRIM(
                            RTRIM(
                                t.Name))) =
                        N'infraestructura y servidores'
            );


            /* =====================================================
               HARDWARE
               ===================================================== */

            INSERT INTO dbo.HelpdeskTeams
            (
                Id,
                OrganizationId,
                Name,
                Description,
                Categories,
                IsActive,
                CreatedAtUtc
            )
            SELECT
                NEWID(),
                o.Id,
                N'Hardware y Periféricos',
                N'Computadoras, monitores, impresoras, escáneres, periféricos y fallas físicas.',
                N'|hardware|desktop|laptop|monitor|impresora|escáner|periféricos|daño físico|',
                1,
                @NowUtc
            FROM dbo.Organizations o
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.HelpdeskTeams t
                WHERE
                    t.OrganizationId = o.Id
                    AND LOWER(
                        LTRIM(
                            RTRIM(
                                t.Name))) =
                        N'hardware y periféricos'
            );


            /* =====================================================
               ANDROID
               ===================================================== */

            INSERT INTO dbo.HelpdeskTeams
            (
                Id,
                OrganizationId,
                Name,
                Description,
                Categories,
                IsActive,
                CreatedAtUtc
            )
            SELECT
                NEWID(),
                o.Id,
                N'Android y Movilidad',
                N'Dispositivos Android, MDM, SIM, conectividad móvil y aplicaciones móviles.',
                N'|android|mdm|sim|datos móviles|aplicación móvil|',
                1,
                @NowUtc
            FROM dbo.Organizations o
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.HelpdeskTeams t
                WHERE
                    t.OrganizationId = o.Id
                    AND LOWER(
                        LTRIM(
                            RTRIM(
                                t.Name))) =
                        N'android y movilidad'
            );


            /* =====================================================
               PONCHES
               ===================================================== */

            INSERT INTO dbo.HelpdeskTeams
            (
                Id,
                OrganizationId,
                Name,
                Description,
                Categories,
                IsActive,
                CreatedAtUtc
            )
            SELECT
                NEWID(),
                o.Id,
                N'Ponches y Biometría',
                N'Relojes biométricos, ZKTeco, marcaciones y sincronización de asistencia.',
                N'|ponches|zkteco|marcación|biometría|reloj offline|sincronización|',
                1,
                @NowUtc
            FROM dbo.Organizations o
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.HelpdeskTeams t
                WHERE
                    t.OrganizationId = o.Id
                    AND LOWER(
                        LTRIM(
                            RTRIM(
                                t.Name))) =
                        N'ponches y biometría'
            );
            """);
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        /*
         * INTENCIONALMENTE NO DESTRUCTIVO.
         *
         * Este migration introduce catálogo empresarial pero los
         * grupos pueden adquirir posteriormente:
         *
         * - técnicos;
         * - horarios;
         * - coberturas;
         * - tickets;
         * - configuración administrativa.
         *
         * Un rollback automático que elimine grupos podría destruir
         * relaciones operativas. Por esa razón Down no elimina datos.
         */
    }
}