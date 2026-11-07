namespace TitanMDM.Infrastructure.Android.Provisioning;

/// <summary>Sección "AndroidProvisioning" de appsettings.</summary>
public sealed class AndroidProvisioningOptions
{
    public const string SectionName = "AndroidProvisioning";

    /// <summary>URL pública del APK del agente que descarga el asistente de Android.</summary>
    public string DownloadUrl { get; set; } =
        "https://raw.githubusercontent.com/eliashughesdev/TITAN/main/deploy/app-debug.apk";

    /// <summary>APK local (opcional). Por defecto se busca deploy/app-debug.apk del repositorio.</summary>
    public string? ApkPath { get; set; }

    /// <summary>Checksum fijo (opcional); si falta se calcula del APK.</summary>
    public string? SignatureChecksum { get; set; }

    public string ComponentName { get; set; } =
        "com.titanmdm.agent/com.titanmdm.agent.admin.TitanDeviceAdminReceiver";
}
