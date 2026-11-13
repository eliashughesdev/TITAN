using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Android.Policies;
using TitanMDM.Application.Applications;
using TitanMDM.Application.Commands;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Android.Apk;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Applications;

public sealed class SoftwareDeploymentService
    : ISoftwareDeploymentService
{
    private readonly TitanMdmDbContext
        _dbContext;

    private readonly IDeviceCommandService
        _commandService;

    private readonly string
        _storageRoot;

    private static readonly HashSet<string>
        AllowedExtensions =
            new(
                new[]
                {
                    ".msi",
                    ".exe",
                    ".msix",
                    ".appx",
                    ".apk"
                },
                StringComparer.OrdinalIgnoreCase);

    private readonly IAndroidPolicyAssignmentService?
        _androidPolicies;

    public SoftwareDeploymentService(
        TitanMdmDbContext dbContext,
        IDeviceCommandService commandService,
        IAndroidPolicyAssignmentService? androidPolicies = null)
    {
        _androidPolicies =
            androidPolicies;

        _dbContext =
            dbContext;

        _commandService =
            commandService;

        _storageRoot =
            SoftwarePackageStorage.Root;

        Directory.CreateDirectory(
            _storageRoot);
    }

    public async Task<SoftwarePackageDto>
        UploadPackageAsync(
            Guid organizationId,
            Guid userId,
            CreateSoftwarePackageRequest request,
            string fileName,
            Stream fileStream,
            CancellationToken cancellationToken = default)
    {
        ValidateUploadRequest(
            organizationId,
            userId,
            request,
            fileName,
            fileStream);

        var extension =
            Path.GetExtension(
                fileName);

        if (
            !AllowedExtensions.Contains(
                extension))
        {
            throw new InvalidOperationException(
                $"El tipo de archivo '{extension}' no está permitido.");
        }

        var normalizedPackageType =
            NormalizePackageType(
                request.PackageType,
                extension);

        var packageId =
            Guid.NewGuid();

        var storedFileName =
            $"{packageId:N}{extension.ToLowerInvariant()}";

        var organizationDirectory =
            Path.Combine(
                _storageRoot,
                organizationId
                    .ToString("N"));

        Directory.CreateDirectory(
            organizationDirectory);

        var relativePath =
            Path.Combine(
                organizationId
                    .ToString("N"),
                storedFileName);

        var fullPath =
            Path.Combine(
                _storageRoot,
                relativePath);

        try
        {
            await using (
                var output =
                    new FileStream(
                        fullPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize:
                            1024 * 128,
                        useAsync:
                            true))
            {
                await fileStream
                    .CopyToAsync(
                        output,
                        cancellationToken);
            }

            var fileInfo =
                new FileInfo(
                    fullPath);

            if (
                !fileInfo.Exists
                ||
                fileInfo.Length <= 0)
            {
                throw new InvalidOperationException(
                    "El paquete recibido está vacío.");
            }

            string? androidPackageName = null;

            if (normalizedPackageType ==
                SoftwarePackageStorage.ApkPackageType)
            {
                androidPackageName =
                    EnsureValidApk(
                        fullPath);
            }

            var sha256 =
                await CalculateSha256Async(
                    fullPath,
                    cancellationToken);

            var package =
                new SoftwarePackage(
                    organizationId:
                        organizationId,

                    name:
                        request.Name,

                    version:
                        request.Version,

                    packageType:
                        normalizedPackageType,

                    originalFileName:
                        Path.GetFileName(
                            fileName),

                    storedFileName:
                        storedFileName,

                    relativePath:
                        relativePath,

                    sha256:
                        sha256,

                    sizeBytes:
                        fileInfo.Length,

                    installArguments:
                        request.InstallArguments,

                    createdByUserId:
                        userId,

                    id:
                        packageId);

            _dbContext
                .SoftwarePackages
                .Add(
                    package);

            await _dbContext
                .SaveChangesAsync(
                    cancellationToken);

            // Equipos del agente que esperaban este APK lo instalan ya.
            // Un fallo aquí no debe deshacer la subida (el APK ya está
            // registrado); la política puede reasignarse desde la consola.
            if (androidPackageName is not null &&
                _androidPolicies is not null)
            {
                try
                {
                    await _androidPolicies
                        .RequeueAgentPoliciesForPackageAsync(
                            organizationId,
                            androidPackageName,
                            userId,
                            cancellationToken);
                }
                catch (Exception exception) when (
                    exception is not OperationCanceledException)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"No se reenviaron políticas tras subir {androidPackageName}: {exception.Message}");
                }
            }

            return MapPackage(
                package);
        }
        catch
        {
            TryDeleteFile(
                fullPath);

            throw;
        }
    }

    public async Task<
        IReadOnlyCollection<SoftwarePackageDto>>
        GetPackagesAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
    {
        ValidateOrganization(
            organizationId);

        var packages =
            await _dbContext
                .SoftwarePackages
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                        organizationId)
                .OrderByDescending(
                    x =>
                        x.CreatedAtUtc)
                .ToListAsync(
                    cancellationToken);

        return packages
            .Select(
                MapPackage)
            .ToArray();
    }

    public async Task<
        IReadOnlyCollection<SoftwareDeploymentDto>>
        GetDeploymentsAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
    {
        ValidateOrganization(
            organizationId);

        var deployments =
            await _dbContext
                .SoftwareDeployments
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                        organizationId)
                .OrderByDescending(
                    x =>
                        x.CreatedAtUtc)
                .Take(500)
                .ToListAsync(
                    cancellationToken);

        if (
            deployments.Count == 0)
        {
            return Array.Empty<
                SoftwareDeploymentDto>();
        }

        var packageIds =
            deployments
                .Select(
                    x =>
                        x.PackageId)
                .Distinct()
                .ToArray();

        var deviceTargetIds =
            deployments
                .Where(
                    x =>
                        x.TargetType.Equals(
                            "Device",
                            StringComparison.OrdinalIgnoreCase))
                .Select(
                    x =>
                        x.TargetId)
                .Distinct()
                .ToArray();

        var groupTargetIds =
            deployments
                .Where(
                    x =>
                        x.TargetType.Equals(
                            "Group",
                            StringComparison.OrdinalIgnoreCase))
                .Select(
                    x =>
                        x.TargetId)
                .Distinct()
                .ToArray();

        var packages =
            await _dbContext
                .SoftwarePackages
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        packageIds.Contains(
                            x.Id))
                .ToDictionaryAsync(
                    x =>
                        x.Id,
                    cancellationToken);

        var devices =
            deviceTargetIds.Length == 0
                ? new Dictionary<
                    Guid,
                    Device>()
                : await _dbContext
                    .Devices
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            deviceTargetIds.Contains(
                                x.Id))
                    .ToDictionaryAsync(
                        x =>
                            x.Id,
                        cancellationToken);

        var groups =
            groupTargetIds.Length == 0
                ? new Dictionary<
                    Guid,
                    DeviceGroup>()
                : await _dbContext
                    .DeviceGroups
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.OrganizationId ==
                                organizationId
                            &&
                            groupTargetIds.Contains(
                                x.Id))
                    .ToDictionaryAsync(
                        x =>
                            x.Id,
                        cancellationToken);

        return deployments
            .Select(
                deployment =>
                {
                    packages.TryGetValue(
                        deployment.PackageId,
                        out var package);

                    var targetName =
                        ResolveTargetName(
                            deployment,
                            devices,
                            groups);

                    return new SoftwareDeploymentDto(
                        deployment.Id,
                        deployment.PackageId,
                        package?.Name ??
                            "Paquete eliminado",
                        package?.Version ??
                            "N/D",
                        deployment.TargetType,
                        deployment.TargetId,
                        targetName,
                        deployment.Status,
                        deployment.QueuedDevices,
                        deployment.CreatedAtUtc);
                })
            .ToArray();
    }

    public async Task<SoftwareDeploymentDto>
        DeployAsync(
            Guid organizationId,
            Guid userId,
            Guid packageId,
            DeploySoftwarePackageRequest request,
            CancellationToken cancellationToken = default)
    {
        ValidateOrganization(
            organizationId);

        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "El usuario no es válido.");
        }

        if (packageId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "PackageId no es válido.");
        }

        if (
            request is null
            ||
            request.TargetId ==
                Guid.Empty
            ||
            string.IsNullOrWhiteSpace(
                request.TargetType))
        {
            throw new InvalidOperationException(
                "El destino del deployment no es válido.");
        }

        var package =
            await _dbContext
                .SoftwarePackages
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            packageId
                        &&
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsActive,
                    cancellationToken)
            ??
            throw new InvalidOperationException(
                "El paquete no existe o está deshabilitado.");

        var packageFilePath =
            GetPackageFullPath(
                package);

        if (
            !File.Exists(
                packageFilePath))
        {
            throw new InvalidOperationException(
                "El archivo físico del paquete no existe en el servidor.");
        }

        var targetType =
            NormalizeTargetType(
                request.TargetType);

        var (
            deviceIds,
            targetName) =
                await ResolveDeploymentTargetsAsync(
                    organizationId,
                    targetType,
                    request.TargetId,
                    SoftwarePackageStorage.TargetPlatform(
                        package.PackageType),
                    cancellationToken);

        if (
            deviceIds.Count == 0)
        {
            var emptyDeployment =
                new SoftwareDeployment(
                    organizationId,
                    package.Id,
                    targetType,
                    request.TargetId,
                    userId);

            emptyDeployment
                .MarkQueued(
                    0);

            _dbContext
                .SoftwareDeployments
                .Add(
                    emptyDeployment);

            await _dbContext
                .SaveChangesAsync(
                    cancellationToken);

            return new SoftwareDeploymentDto(
                emptyDeployment.Id,
                package.Id,
                package.Name,
                package.Version,
                targetType,
                request.TargetId,
                targetName,
                emptyDeployment.Status,
                0,
                emptyDeployment.CreatedAtUtc);
        }

        var deployment =
            new SoftwareDeployment(
                organizationId,
                package.Id,
                targetType,
                request.TargetId,
                userId);

        _dbContext
            .SoftwareDeployments
            .Add(
                deployment);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        var queuedDevices =
            0;

        foreach (
            var deviceId
            in deviceIds)
        {
            var androidPackageName =
                ReadAndroidPackageName(
                    package);

        var payload =
                JsonSerializer.Serialize(
                    new
                    {
                        packageId =
                            package.Id,

                        packageName =
                            androidPackageName,

                        downloadUrl =
                            androidPackageName is null
                                ? $"/api/device/software-packages/{package.Id}/download"
                                : ManagedAppDownload.Url(
                                    androidPackageName),

                        fileName =
                            package.OriginalFileName,

                        expectedSha256 =
                            package.Sha256,

                        arguments =
                            package.InstallArguments,

                        timeoutSeconds =
                            1800
                    });

            await _commandService
                .CreateAsync(
                    organizationId,
                    userId,
                    new CreateDeviceCommandRequest(
                        DeviceId:
                            deviceId,

                        CommandType:
                            "SOFTWARE_INSTALL",

                        PayloadJson:
                            payload,

                        ExpirationMinutes:
                            60),
                    cancellationToken);

            queuedDevices++;
        }

        deployment
            .MarkQueued(
                queuedDevices);

        await _dbContext
            .SaveChangesAsync(
                cancellationToken);

        return new SoftwareDeploymentDto(
            deployment.Id,
            package.Id,
            package.Name,
            package.Version,
            targetType,
            request.TargetId,
            targetName,
            deployment.Status,
            deployment.QueuedDevices,
            deployment.CreatedAtUtc);
    }

    public async Task<
        SoftwarePackageDownloadDto?>
        GetPackageDownloadAsync(
            Guid deviceId,
            Guid packageId,
            CancellationToken cancellationToken = default)
    {
        if (
            deviceId == Guid.Empty
            ||
            packageId == Guid.Empty)
        {
            return null;
        }

        var device =
            await _dbContext
                .Devices
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            deviceId
                        &&
                        !x.IsDeleted,
                    cancellationToken);

        if (
            device is null
            ||
            (device.Platform !=
                DevicePlatform.Windows
             &&
             device.Platform !=
                DevicePlatform.Android))
        {
            return null;
        }

        var package =
            await _dbContext
                .SoftwarePackages
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            packageId
                        &&
                        x.OrganizationId ==
                            device.OrganizationId
                        &&
                        x.IsActive,
                    cancellationToken);

        if (
            package is null
            ||
            SoftwarePackageStorage.TargetPlatform(
                package.PackageType) !=
                device.Platform)
        {
            return null;
        }

        /*
         * Solo permitimos la descarga si existe
         * un comando del dispositivo que referencia
         * este packageId.
         *
         * SOFTWARE_INSTALL es una autorización puntual.
         * APPLY_LAUNCHER_POLICY se conserva como
         * autorización de la política asignada durante
         * 30 días para que el launcher pueda reintentar
         * la instalación silenciosa sin intervención.
         *
         * Esto evita que un agente autenticado pueda
         * descargar paquetes arbitrarios del tenant.
         */
        var packageIdText =
            packageId.ToString();

        var now =
            DateTime.UtcNow;

        var authorized =
            await _dbContext
                .DeviceCommands
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.DeviceId ==
                            deviceId
                        &&
                        x.OrganizationId ==
                            device.OrganizationId
                        &&
                        x.PayloadJson.Contains(
                            packageIdText)
                        &&
                        (
                            (x.CommandType ==
                                "SOFTWARE_INSTALL"
                             &&
                             x.ExpiresAtUtc >
                                now)
                            ||
                            (x.CommandType ==
                                "APPLY_LAUNCHER_POLICY"
                             &&
                             (x.CreatedAtUtc >
                                now.AddDays(-30)
                              ||
                              _dbContext.DevicePolicyAssignments
                                  .Any(assignment =>
                                      assignment.DeviceId ==
                                          deviceId
                                      &&
                                      assignment.OrganizationId ==
                                          device.OrganizationId)))
                        ),
                    cancellationToken);

        if (!authorized)
        {
            return null;
        }

        var fullPath =
            GetPackageFullPath(
                package);

        if (
            !File.Exists(
                fullPath))
        {
            return null;
        }

        return new SoftwarePackageDownloadDto(
            FullPath:
                fullPath,

            FileName:
                package.OriginalFileName,

            ContentType:
                GetContentType(
                    package.PackageType));
    }

    public async Task<SoftwarePackageDownloadDto?>
        GetApkDownloadByPackageNameAsync(
            Guid deviceId,
            string packageName,
            CancellationToken cancellationToken = default)
    {
        if (
            deviceId == Guid.Empty
            ||
            !ManagedAppDownload.IsValidPackageName(
                packageName))
        {
            return null;
        }

        var device =
            await _dbContext
                .Devices
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == deviceId
                        &&
                        !x.IsDeleted,
                    cancellationToken);

        if (
            device is null
            ||
            device.Platform != DevicePlatform.Android)
        {
            return null;
        }

        var packages =
            await _dbContext
                .SoftwarePackages
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId == device.OrganizationId
                    &&
                    x.IsActive
                    &&
                    x.PackageType == SoftwarePackageStorage.ApkPackageType)
                .ToListAsync(cancellationToken);

        SoftwarePackage? match = null;
        long versionCode = -1;

        foreach (var candidate in packages)
        {
            var path = GetPackageFullPath(candidate);
            if (!File.Exists(path))
                continue;

            ApkManifestInfo manifest;
            try
            {
                manifest = ApkInspector.ReadManifest(path);
                ApkInspector.ReadSigningCertificate(path);
            }
            catch (Exception exception) when (
                exception is InvalidDataException
                    or IOException
                    or InvalidOperationException
                    or CryptographicException)
            {
                continue;
            }

            if (!string.Equals(
                    manifest.PackageName,
                    packageName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (manifest.VersionCode < versionCode)
                continue;

            if (
                manifest.VersionCode == versionCode
                &&
                match is not null
                &&
                candidate.CreatedAtUtc <= match.CreatedAtUtc)
            {
                continue;
            }

            match = candidate;
            versionCode = manifest.VersionCode;
        }

        if (match is null)
            return null;

        var quotedName = "\"" + packageName + "\"";
        var packageIdText = match.Id.ToString();
        var now = DateTime.UtcNow;

        var authorized =
            await _dbContext
                .DeviceCommands
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.DeviceId == deviceId
                        &&
                        x.OrganizationId == device.OrganizationId
                        &&
                        (x.PayloadJson.Contains(quotedName)
                         ||
                         x.PayloadJson.Contains(packageIdText))
                        &&
                        (
                            (x.CommandType == "SOFTWARE_INSTALL"
                             &&
                             x.ExpiresAtUtc > now)
                            ||
                            (x.CommandType == "APPLY_LAUNCHER_POLICY"
                             &&
                             (x.CreatedAtUtc > now.AddDays(-30)
                              ||
                              _dbContext.DevicePolicyAssignments
                                  .Any(assignment =>
                                      assignment.DeviceId == deviceId
                                      &&
                                      assignment.OrganizationId == device.OrganizationId)))
                        ),
                    cancellationToken);

        if (!authorized)
            return null;

        var fullPath = GetPackageFullPath(match);
        if (!File.Exists(fullPath))
            return null;

        return new SoftwarePackageDownloadDto(
            fullPath,
            match.OriginalFileName,
            GetContentType(match.PackageType));
    }

    private string? ReadAndroidPackageName(SoftwarePackage package)
    {
        if (!string.Equals(
                package.PackageType,
                SoftwarePackageStorage.ApkPackageType,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var path = GetPackageFullPath(package);
            if (!File.Exists(path))
                return null;

            var manifest = ApkInspector.ReadManifest(path);
            ApkInspector.ReadSigningCertificate(path);
            return ManagedAppDownload.IsValidPackageName(manifest.PackageName)
                ? manifest.PackageName
                : null;
        }
        catch (Exception exception) when (
            exception is InvalidDataException
                or IOException
                or InvalidOperationException
                or CryptographicException)
        {
            return null;
        }
    }

    private async Task<
    (
        List<Guid> DeviceIds,
        string TargetName
    )>
    ResolveDeploymentTargetsAsync(
        Guid organizationId,
        string targetType,
        Guid targetId,
        DevicePlatform targetPlatform,
        CancellationToken cancellationToken)
    {
        if (
            targetType ==
            "Device")
        {
            var device =
                await _dbContext
                    .Devices
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        x =>
                            x.Id ==
                                targetId
                            &&
                            x.OrganizationId ==
                                organizationId
                            &&
                            !x.IsDeleted,
                        cancellationToken)
                ??
                throw new InvalidOperationException(
                    "El dispositivo no existe.");

            if (
                device.Platform !=
                targetPlatform)
            {
                throw new InvalidOperationException(
                    targetPlatform == DevicePlatform.Android
                        ? "Los paquetes APK solo pueden desplegarse a dispositivos Android."
                        : "Los paquetes Windows solo pueden desplegarse a dispositivos Windows.");
            }

            return (
                new List<Guid>
                {
                device.Id
                },
                device.DeviceName);
        }

        var deviceGroup =
            await _dbContext
                .DeviceGroups
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            targetId
                        &&
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.IsEnabled,
                    cancellationToken)
            ??
            throw new InvalidOperationException(
                "El grupo no existe o está deshabilitado.");

        var deviceIds =
            await (
                from member
                    in _dbContext
                        .DeviceGroupMembers
                        .AsNoTracking()

                join device
                    in _dbContext
                        .Devices
                        .AsNoTracking()

                    on member.DeviceId
                    equals device.Id

                where
                    member.OrganizationId ==
                        organizationId
                    &&
                    member.GroupId ==
                        deviceGroup.Id
                    &&
                    device.OrganizationId ==
                        organizationId
                    &&
                    device.Platform ==
                        targetPlatform
                    &&
                    !device.IsDeleted

                select device.Id
            )
            .Distinct()
            .ToListAsync(
                cancellationToken);

        return (
            deviceIds,
            deviceGroup.Name);
    }

    private static string
        ResolveTargetName(
            SoftwareDeployment deployment,
            IReadOnlyDictionary<
                Guid,
                Device> devices,
            IReadOnlyDictionary<
                Guid,
                DeviceGroup> groups)
    {
        if (
            deployment.TargetType.Equals(
                "Group",
                StringComparison.OrdinalIgnoreCase))
        {
            return groups
                .TryGetValue(
                    deployment.TargetId,
                    out var group)
                ? group.Name
                : "Grupo no disponible";
        }

        return devices
            .TryGetValue(
                deployment.TargetId,
                out var device)
            ? device.DeviceName
            : "Dispositivo no disponible";
    }

    private string GetPackageFullPath(
        SoftwarePackage package)
    {
        var root =
            Path.GetFullPath(
                _storageRoot);

        var fullPath =
            Path.GetFullPath(
                Path.Combine(
                    root,
                    package.RelativePath));

        var rootWithSeparator =
            root.EndsWith(
                Path.DirectorySeparatorChar)
                ? root
                : root +
                  Path.DirectorySeparatorChar;

        if (
            !fullPath.StartsWith(
                rootWithSeparator,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "La ruta del paquete no es válida.");
        }

        return fullPath;
    }

    private static SoftwarePackageDto
        MapPackage(
            SoftwarePackage package)
    {
        return new SoftwarePackageDto(
            package.Id,
            package.Name,
            package.Version,
            package.PackageType,
            package.OriginalFileName,
            package.Sha256,
            package.SizeBytes,
            package.InstallArguments,
            package.IsActive,
            package.CreatedAtUtc);
    }

    private static void
        ValidateUploadRequest(
            Guid organizationId,
            Guid userId,
            CreateSoftwarePackageRequest request,
            string fileName,
            Stream fileStream)
    {
        ValidateOrganization(
            organizationId);

        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "El usuario no es válido.");
        }

        ArgumentNullException.ThrowIfNull(
            request);

        ArgumentNullException.ThrowIfNull(
            fileStream);

        if (
            string.IsNullOrWhiteSpace(
                request.Name))
        {
            throw new InvalidOperationException(
                "El nombre del software es obligatorio.");
        }

        if (
            string.IsNullOrWhiteSpace(
                request.Version))
        {
            throw new InvalidOperationException(
                "La versión es obligatoria.");
        }

        if (
            string.IsNullOrWhiteSpace(
                request.PackageType))
        {
            throw new InvalidOperationException(
                "PackageType es obligatorio.");
        }

        if (
            string.IsNullOrWhiteSpace(
                fileName))
        {
            throw new InvalidOperationException(
                "El nombre del archivo es obligatorio.");
        }

        if (
            !fileStream.CanRead)
        {
            throw new InvalidOperationException(
                "El archivo no puede ser leído.");
        }
    }

    private static void
        ValidateOrganization(
            Guid organizationId)
    {
        if (
            organizationId ==
            Guid.Empty)
        {
            throw new InvalidOperationException(
                "La organización no es válida.");
        }
    }

    private static string
        NormalizePackageType(
            string packageType,
            string extension)
    {
        var normalized =
            packageType
                .Trim()
                .ToUpperInvariant();

        var expected =
            extension
                .TrimStart('.')
                .ToUpperInvariant();

        if (
            normalized !=
            expected)
        {
            throw new InvalidOperationException(
                $"PackageType '{normalized}' no coincide con el archivo '{extension}'.");
        }

        return normalized;
    }

    private static string
        NormalizeTargetType(
            string targetType)
    {
        if (
            targetType.Equals(
                "Device",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Device";
        }

        if (
            targetType.Equals(
                "Group",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Group";
        }

        throw new InvalidOperationException(
            "TargetType debe ser Device o Group.");
    }

    private static string
        GetContentType(
            string packageType)
    {
        return packageType
            .ToUpperInvariant()
            switch
        {
            "MSI" =>
                "application/x-msi",

            "MSIX" =>
                "application/msix",

            "APPX" =>
                "application/appx",

            "APK" =>
                "application/vnd.android.package-archive",

            _ =>
                "application/octet-stream"
        };
    }

    /// <summary>El APK debe tener manifiesto legible y estar firmado.</summary>
    private static string EnsureValidApk(
        string fullPath)
    {
        try
        {
            var manifest =
                ApkInspector.ReadManifest(
                    fullPath);

            ApkInspector.ReadSigningCertificate(
                fullPath);

            return manifest.PackageName;
        }
        catch (Exception exception) when (
            exception is InvalidDataException or IOException or ArgumentException or System.Security.Cryptography.CryptographicException)
        {
            throw new InvalidOperationException(
                $"El archivo no es un APK Android válido: {exception.Message}");
        }
    }

    private static async Task<string>
        CalculateSha256Async(
            string fullPath,
            CancellationToken cancellationToken)
    {
        await using var stream =
            new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize:
                    1024 * 128,
                useAsync:
                    true);

        var hash =
            await SHA256
                .HashDataAsync(
                    stream,
                    cancellationToken);

        return Convert
            .ToHexString(
                hash);
    }

    private static void TryDeleteFile(
        string fullPath)
    {
        try
        {
            if (
                File.Exists(
                    fullPath))
            {
                File.Delete(
                    fullPath);
            }
        }
        catch
        {
            // No ocultamos la excepción original.
        }
    }
}