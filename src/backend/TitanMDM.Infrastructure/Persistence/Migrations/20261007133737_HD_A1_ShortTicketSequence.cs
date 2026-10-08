using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TitanMDM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HD_A1_ShortTicketSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(
           MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
        /*
         * ============================================================
         * TITANMDM HELPDESK
         * SHORT SEQUENTIAL TICKET NUMBERS
         * ============================================================
         *
         * Ambiente existente:
         *
         *   HD-20261007-XXXX
         *
         * pasa a:
         *
         *   HD-1
         *   HD-2
         *   ...
         *
         * Después SQL Server administra la secuencia.
         * ============================================================
         */

        IF OBJECT_ID('dbo.HelpdeskTickets', 'U') IS NOT NULL
        BEGIN
            /*
             * Renumeración determinística de tickets existentes.
             *
             * Primero utilizamos TMP para evitar colisiones con
             * cualquier número HD-N que ya pudiera existir.
             */

            ;WITH OrderedTickets AS
            (
                SELECT
                    Id,
                    ROW_NUMBER() OVER
                    (
                        ORDER BY
                            CreatedAtUtc ASC,
                            Id ASC
                    ) AS SequenceNumber
                FROM dbo.HelpdeskTickets
            )
            UPDATE Ticket
            SET Number =
                CONCAT(
                    'TMP-HD-',
                    Ordered.SequenceNumber
                )
            FROM dbo.HelpdeskTickets AS Ticket
            INNER JOIN OrderedTickets AS Ordered
                ON Ordered.Id =
                   Ticket.Id;

            UPDATE dbo.HelpdeskTickets
            SET Number =
                REPLACE(
                    Number,
                    'TMP-HD-',
                    'HD-')
            WHERE Number LIKE
                  'TMP-HD-%';
        END;

        /*
         * Eliminar secuencia previa solamente si existe.
         *
         * Esta migración todavía está en desarrollo y queremos
         * garantizar una instalación determinística.
         */

        IF EXISTS
        (
            SELECT 1
            FROM sys.sequences
            WHERE
                name =
                    'HelpdeskTicketNumberSequence'
                AND schema_id =
                    SCHEMA_ID('dbo')
        )
        BEGIN
            DROP SEQUENCE
                dbo.HelpdeskTicketNumberSequence;
        END;

        /*
         * Calcular el próximo número.
         */

        DECLARE @NextTicketNumber BIGINT;

        SELECT
            @NextTicketNumber =
                ISNULL(
                    MAX(
                        TRY_CONVERT(
                            BIGINT,
                            REPLACE(
                                Number,
                                'HD-',
                                '')
                        )
                    ),
                    0
                )
                + 1
        FROM dbo.HelpdeskTickets
        WHERE Number LIKE 'HD-%';

        IF @NextTicketNumber < 1
            SET @NextTicketNumber = 1;

        /*
         * CREATE SEQUENCE no admite una variable directamente
         * en START WITH, por eso usamos SQL dinámico controlado.
         */

        DECLARE @SequenceSql NVARCHAR(MAX);

        SET @SequenceSql =
            N'CREATE SEQUENCE dbo.HelpdeskTicketNumberSequence ' +
            N'AS BIGINT ' +
            N'START WITH ' +
            CONVERT(
                NVARCHAR(30),
                @NextTicketNumber) +
            N' INCREMENT BY 1 ' +
            N'MINVALUE 1 ' +
            N'NO MAXVALUE ' +
            N'CACHE 50;';

        EXEC sys.sp_executesql
            @SequenceSql;
        """);
        }

        protected override void Down(
            MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
        IF EXISTS
        (
            SELECT 1
            FROM sys.sequences
            WHERE
                name =
                    'HelpdeskTicketNumberSequence'
                AND schema_id =
                    SCHEMA_ID('dbo')
        )
        BEGIN
            DROP SEQUENCE
                dbo.HelpdeskTicketNumberSequence;
        END;
        """);
        }
    }
}
