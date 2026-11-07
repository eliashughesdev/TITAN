using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;

namespace TitanMDM.Infrastructure.Applications;

/// <summary>Ubicación física y plataforma de los paquetes de software.</summary>
public static class SoftwarePackageStorage
{
    public const string ApkPackageType = "APK";

    public static string Root { get; } =
        Path.Combine(AppContext.BaseDirectory, "App_Data", "software");

    /// <summary>Ruta absoluta del paquete; rechaza rutas fuera del almacén.</summary>
    public static string GetFullPath(SoftwarePackage package)
        => GetFullPath(Root, package.RelativePath);

    public static string GetFullPath(string storageRoot, string relativePath)
    {
        var root = Path.GetFullPath(storageRoot);
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La ruta del paquete no es válida.");

        return fullPath;
    }

    /// <summary>APK → Android; MSI/EXE/MSIX/APPX → Windows.</summary>
    public static DevicePlatform TargetPlatform(string packageType)
        => string.Equals(packageType, ApkPackageType, StringComparison.OrdinalIgnoreCase)
            ? DevicePlatform.Android
            : DevicePlatform.Windows;
}
