
using TitanMDM.RemoteHost.Interop;
using TitanMDM.RemoteHost.Models;
using TitanMDM.RemoteHost.UI;

namespace TitanMDM.RemoteHost;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        try
        {
            var session = ParseSessionAsync(args)
                .GetAwaiter()
                .GetResult();

            Application.Run(
                new RemoteSessionIndicatorForm(session));
        }
        catch (Exception ex)
        {
            // No mostramos datos del bootstrap ni argumentos.
            MessageBox.Show(
                $"No fue posible iniciar RemoteHost: {ex.GetType().Name}",
                "TitanMDM Remote Host",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static async Task<RemoteHostSession>
        ParseSessionAsync(string[] args)
    {
        var index = Array.FindIndex(
            args,
            item => string.Equals(
                item,
                "--bootstrap-pipe",
                StringComparison.OrdinalIgnoreCase));

        if (index < 0 || index + 1 >= args.Length)
        {
            throw new InvalidOperationException(
                "RemoteHost requiere un canal bootstrap autorizado.");
        }

        var pipeName = args[index + 1];

        return await RemoteHostBootstrapPipeClient.ReceiveAsync(
            pipeName);
    }
}
