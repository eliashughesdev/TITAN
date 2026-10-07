using System.Text;
using System.Text.Json;
using TitanMDM.RemoteHost.Models;
using TitanMDM.RemoteHost.UI;

namespace TitanMDM.RemoteHost;

internal static class Program
{
    [STAThread]
    private static void Main(
        string[] args)
    {
        ApplicationConfiguration.Initialize();

        try
        {
            var session =
                ParseSession(
                    args);

            Application.Run(
                new RemoteSessionIndicatorForm(
                    session));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "TitanMDM Remote Host",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static RemoteHostSession
        ParseSession(
            string[] args)
    {
        var index =
            Array.FindIndex(
                args,
                argument =>
                    string.Equals(
                        argument,
                        "--session",
                        StringComparison.OrdinalIgnoreCase));

        if (index < 0 ||
            index + 1 >= args.Length)
        {
            throw new InvalidOperationException(
                "No se recibió la configuración de la sesión remota.");
        }

        var encodedPayload =
            args[index + 1];

        byte[] payloadBytes;

        try
        {
            payloadBytes =
                Convert.FromBase64String(
                    encodedPayload);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "La configuración de la sesión remota posee un formato inválido.",
                ex);
        }

        var json =
            Encoding.UTF8.GetString(
                payloadBytes);

        var session =
            JsonSerializer.Deserialize<
                RemoteHostSession>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive =
                        true
                });

        return session
            ?? throw new InvalidOperationException(
                "TitanMDM no pudo interpretar la configuración de Remote Support.");
    }
}