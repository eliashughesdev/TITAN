using System.Xml.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TitanMDM.Infrastructure.Persistence;

public sealed class TitanMdmDbContextFactory
    : IDesignTimeDbContextFactory<TitanMdmDbContext>
{
    public TitanMdmDbContext CreateDbContext(
        string[] args)
    {
        var apiDirectory =
            ResolveApiDirectory();

        var environment =
            Environment.GetEnvironmentVariable(
                "ASPNETCORE_ENVIRONMENT")
            ?? "Development";

        var configurationBuilder =
            new ConfigurationBuilder()
                .SetBasePath(
                    apiDirectory)
                .AddJsonFile(
                    "appsettings.json",
                    optional: false,
                    reloadOnChange: false)
                .AddJsonFile(
                    $"appsettings.{environment}.json",
                    optional: true,
                    reloadOnChange: false);

        /*
         * ==========================================================
         * USER SECRETS
         * ==========================================================
         *
         * EF Core design-time no ejecuta Program.cs.
         *
         * Por tanto, aunque TitanMDM.Api tenga:
         *
         * <UserSecretsId>...</UserSecretsId>
         *
         * el factory de Infrastructure no recibe automáticamente
         * esos secretos.
         *
         * Los cargamos de forma explícita únicamente en Development.
         * ==========================================================
         */

        if (
            environment.Equals(
                "Development",
                StringComparison.OrdinalIgnoreCase))
        {
            var userSecretsId =
                ResolveUserSecretsId(
                    apiDirectory);

            if (!string.IsNullOrWhiteSpace(
                    userSecretsId))
            {
                var secretsFile =
                    ResolveSecretsFilePath(
                        userSecretsId);

                if (
                    !string.IsNullOrWhiteSpace(
                        secretsFile)
                    &&
                    File.Exists(
                        secretsFile))
                {
                    configurationBuilder
                        .AddJsonFile(
                            secretsFile,
                            optional: true,
                            reloadOnChange: false);
                }
            }
        }

        /*
         * Environment variables van después de JSON/UserSecrets
         * para permitir override explícito.
         */
        configurationBuilder
            .AddEnvironmentVariables();

        var configuration =
            configurationBuilder.Build();

        var connectionString =
            ResolveConnectionString(
                configuration,
                args);

        var databaseOptions =
            configuration
                .GetSection(
                    DatabaseOptions.SectionName)
                .Get<DatabaseOptions>()
            ??
            new DatabaseOptions();

        var options =
            new DbContextOptionsBuilder<
                TitanMdmDbContext>();

        options.UseSqlServer(
            connectionString,
            sql =>
            {
                sql.CommandTimeout(
                    Math.Max(
                        30,
                        databaseOptions
                            .CommandTimeoutSeconds));

                sql.EnableRetryOnFailure(
                    maxRetryCount:
                        Math.Max(
                            0,
                            databaseOptions
                                .MaxRetryCount),

                    maxRetryDelay:
                        TimeSpan.FromSeconds(
                            Math.Max(
                                1,
                                databaseOptions
                                    .MaxRetryDelaySeconds)),

                    errorNumbersToAdd:
                        null);
            });

        return new TitanMdmDbContext(
            options.Options);
    }

    // ============================================================
    // API DIRECTORY
    // ============================================================

    private static string
        ResolveApiDirectory()
    {
        var current =
            new DirectoryInfo(
                Directory.GetCurrentDirectory());

        while (current is not null)
        {
            /*
             * Ejecutando desde raíz:
             *
             * C:\TitanMDM
             */
            var repoApi =
                Path.Combine(
                    current.FullName,
                    "src",
                    "backend",
                    "TitanMDM.Api");

            if (Directory.Exists(
                    repoApi))
            {
                return repoApi;
            }

            /*
             * Ejecutando desde:
             *
             * C:\TitanMDM\src\backend
             *
             * o Infrastructure.
             */
            var siblingApi =
                Path.Combine(
                    current.FullName,
                    "TitanMDM.Api");

            if (Directory.Exists(
                    siblingApi))
            {
                return siblingApi;
            }

            current =
                current.Parent;
        }

        throw new DirectoryNotFoundException(
            "No fue posible localizar TitanMDM.Api. " +
            "Ejecute dotnet ef desde el repositorio TitanMDM " +
            "o desde src/backend.");
    }

    // ============================================================
    // CONNECTION STRING
    // ============================================================

    private static string
        ResolveConnectionString(
            IConfiguration configuration,
            string[] args)
    {
        /*
         * Prioridad:
         *
         * 1. --connection
         * 2. Environment variable
         * 3. User Secrets
         * 4. appsettings.*
         */

        var argumentValue =
            GetArgumentValue(
                args,
                "--connection");

        if (!string.IsNullOrWhiteSpace(
                argumentValue))
        {
            return argumentValue.Trim();
        }

        var configured =
            configuration
                .GetConnectionString(
                    "TitanMdmDatabase");

        if (!string.IsNullOrWhiteSpace(
                configured))
        {
            return configured.Trim();
        }

        throw new InvalidOperationException(
            "ConnectionStrings:TitanMdmDatabase no está configurado " +
            "para operaciones EF Core design-time. " +
            "Configure User Secrets, la variable " +
            "'ConnectionStrings__TitanMdmDatabase', o utilice " +
            "'--connection <connection-string>' después de '--'.");
    }

    // ============================================================
    // USER SECRETS
    // ============================================================

    private static string?
        ResolveUserSecretsId(
            string apiDirectory)
    {
        var projectFile =
            Path.Combine(
                apiDirectory,
                "TitanMDM.Api.csproj");

        if (!File.Exists(
                projectFile))
        {
            return null;
        }

        try
        {
            var document =
                XDocument.Load(
                    projectFile);

            return document
                .Descendants(
                    "UserSecretsId")
                .Select(
                    x =>
                        x.Value.Trim())
                .FirstOrDefault(
                    x =>
                        !string.IsNullOrWhiteSpace(
                            x));
        }
        catch
        {
            /*
             * La falta de User Secrets no debe impedir que
             * EF utilice environment variables o --connection.
             */
            return null;
        }
    }

    private static string?
        ResolveSecretsFilePath(
            string userSecretsId)
    {
        if (string.IsNullOrWhiteSpace(
                userSecretsId))
        {
            return null;
        }

        /*
         * Windows:
         *
         * %APPDATA%\Microsoft\UserSecrets\<id>\secrets.json
         */

        if (OperatingSystem.IsWindows())
        {
            var appData =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData);

            if (string.IsNullOrWhiteSpace(
                    appData))
            {
                return null;
            }

            return Path.Combine(
                appData,
                "Microsoft",
                "UserSecrets",
                userSecretsId,
                "secrets.json");
        }

        /*
         * Linux / macOS:
         *
         * ~/.microsoft/usersecrets/<id>/secrets.json
         */

        var home =
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrWhiteSpace(
                home))
        {
            return null;
        }

        return Path.Combine(
            home,
            ".microsoft",
            "usersecrets",
            userSecretsId,
            "secrets.json");
    }

    // ============================================================
    // CLI ARGUMENTS
    // ============================================================

    private static string?
        GetArgumentValue(
            IReadOnlyList<string> args,
            string name)
    {
        for (
            var index = 0;
            index < args.Count;
            index++)
        {
            var argument =
                args[index];

            /*
             * Forma:
             *
             * --connection value
             */

            if (
                string.Equals(
                    argument,
                    name,
                    StringComparison.OrdinalIgnoreCase)
                &&
                index + 1 <
                    args.Count)
            {
                return args[
                    index + 1];
            }

            /*
             * Forma:
             *
             * --connection=value
             */

            var prefix =
                name + "=";

            if (
                argument.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return argument[
                    prefix.Length..];
            }
        }

        return null;
    }
}