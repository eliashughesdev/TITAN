using System.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed class HelpdeskTicketNumberGenerator
{
    private readonly TitanMdmDbContext
        _db;

    public HelpdeskTicketNumberGenerator(
        TitanMdmDbContext db)
    {
        _db =
            db;
    }

    public async Task<string> NextAsync(
        CancellationToken cancellationToken = default)
    {
        var connection =
            _db.Database
                .GetDbConnection();

        var shouldClose =
            connection.State !=
            ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(
                cancellationToken);
        }

        try
        {
            await using var command =
                connection.CreateCommand();

            command.CommandText =
                """
                SELECT NEXT VALUE FOR dbo.HelpdeskTicketNumberSequence;
                """;

            var efTransaction =
                _db.Database
                    .CurrentTransaction;

            if (efTransaction is not null)
            {
                command.Transaction =
                    efTransaction
                        .GetDbTransaction();
            }

            var result =
                await command.ExecuteScalarAsync(
                    cancellationToken);

            if (
                result is null
                ||
                result == DBNull.Value)
            {
                throw new InvalidOperationException(
                    "SQL Server no devolvió el siguiente número de ticket.");
            }

            var value =
                Convert.ToInt64(
                    result);

            if (value <= 0)
            {
                throw new InvalidOperationException(
                    "SQL Server devolvió un número de ticket inválido.");
            }

            return $"HD-{value}";
        }
        finally
        {
            if (
                shouldClose
                &&
                connection.State ==
                    ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }
}