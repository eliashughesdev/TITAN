using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using TitanMDM.Application.AndroidEnterprise;
using TitanMDM.Application.Automation;
using TitanMDM.Application.Commands;
using TitanMDM.Application.Enrollment;
using TitanMDM.Application.Enrollment.DeviceRegistration;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Android;
using TitanMDM.Infrastructure.Android.Apk;
using TitanMDM.Infrastructure.Android.Policies;
using TitanMDM.Infrastructure.Applications;
using TitanMDM.Infrastructure.Commands;
using TitanMDM.Infrastructure.Enrollment;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.IntegrationTests.Android;

/*
 * QR de fábrica sin intervención: la credencial lleva la política, el
 * agente se registra con el token del QR y el servidor le encola la
 * política con las apps (APK del repositorio) que debe instalar.
 */
public sealed class ZeroTouchProvisioningTests
{
    private const string SamplePackage = "com.titanmdm.e2e.sample";

    private static string SampleApk =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "android", "titan-e2e-sample.apk");

    private static string AgentApk([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", "deploy", "app-debug.apk"));

    // ============================================================
    // APK
    // ============================================================

    [Fact]
    public void ApkInspector_lee_paquete_version_y_checksum_del_agente()
    {
        var manifest = ApkInspector.ReadManifest(AgentApk());

        Assert.Equal("com.titanmdm.agent", manifest.PackageName);
        Assert.True(manifest.VersionCode >= 1);
        Assert.False(string.IsNullOrWhiteSpace(manifest.VersionName));

        // Mismo valor que apksigner (certificado debug del agente).
        Assert.Equal("gl3pIraiyGi6XKNtLaHjZDcoIBPPgDLWorUdzmRfEcA", ApkInspector.ReadSignatureChecksum(AgentApk()));
    }

    [Fact]
    public void ApkInspector_lee_un_apk_de_politica()
    {
        var manifest = ApkInspector.ReadManifest(SampleApk);

        Assert.Equal(SamplePackage, manifest.PackageName);
        Assert.Equal(7, manifest.VersionCode);
        Assert.Equal("1.7", manifest.VersionName);
        Assert.Equal(43, ApkInspector.ReadSignatureChecksum(SampleApk).Length);
    }

    [Fact]
    public void ApkInspector_rechaza_archivos_que_no_son_apk()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "no es un zip");
            Assert.ThrowsAny<Exception>(() => ApkInspector.ReadManifest(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ============================================================
    // CREDENCIAL CON POLÍTICA
    // ============================================================

    [Fact]
    public async Task Credencial_android_guarda_la_politica_publicada()
    {
        await using var db = Open();
        var (organization, user) = await SeedOrganizationAsync(db);
        var policy = await SeedPolicyAsync(db, organization.Id, user.Id, published: true);

        var created = await new EnrollmentService(db).CreateTokenAsync(
            organization.Id, user.Id, new CreateEnrollmentTokenRequest("Android", 60, 5, policy.Id));

        Assert.Equal(policy.Id, created.AndroidPolicyId);
        Assert.Equal(policy.Name, created.AndroidPolicyName);
        Assert.Equal(policy.Id, (await db.EnrollmentTokens.SingleAsync()).AndroidPolicyId);
    }

    [Fact]
    public async Task Credencial_rechaza_politica_sin_publicar_o_de_otra_plataforma()
    {
        await using var db = Open();
        var (organization, user) = await SeedOrganizationAsync(db);
        var draft = await SeedPolicyAsync(db, organization.Id, user.Id, published: false);
        var service = new EnrollmentService(db);

        var unpublished = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTokenAsync(organization.Id, user.Id, new CreateEnrollmentTokenRequest("Android", 60, 1, draft.Id)));
        Assert.Contains("Publique", unpublished.Message);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTokenAsync(organization.Id, user.Id, new CreateEnrollmentTokenRequest("Windows", 60, 1, draft.Id)));

        Assert.Empty(db.EnrollmentTokens);
    }

    // ============================================================
    // INSCRIPCIÓN → POLÍTICA AUTOMÁTICA
    // ============================================================

    [Fact]
    public async Task Inscripcion_con_token_del_qr_encola_la_politica_con_sus_apps()
    {
        await using var db = Open();
        var (organization, user) = await SeedOrganizationAsync(db);
        var policy = await SeedPolicyAsync(db, organization.Id, user.Id, published: true);
        var created = await new EnrollmentService(db).CreateTokenAsync(
            organization.Id, user.Id, new CreateEnrollmentTokenRequest("Android", 60, 5, policy.Id));
        var apk = await SeedApkPackageAsync(db, organization.Id, user.Id);

        var result = await CreateRegistrationService(db).RegisterAsync(NewRequest(created.Token, "CK65-0001"));

        var device = await db.Devices.SingleAsync();
        Assert.Equal(result.DeviceId, device.Id);
        Assert.True(device.IsManaged);

        var command = await db.DeviceCommands.SingleAsync();
        Assert.Equal(device.Id, command.DeviceId);
        Assert.Equal(AndroidPolicyAssignmentService.AgentLauncherCommandType, command.CommandType);
        Assert.Equal(user.Id, command.CreatedByUserId);

        var payload = JsonNode.Parse(command.PayloadJson)!.AsObject();
        Assert.Equal(policy.Id.ToString("D"), payload["policyId"]!.GetValue<string>());
        Assert.Equal("CISA", payload["launcher"]!["titan_company_name"]!.GetValue<string>());

        var apps = payload["applications"]!.AsArray().Select(x => x!.AsObject()).ToList();
        var sample = Assert.Single(apps, x => x["packageName"]!.GetValue<string>() == SamplePackage);
        Assert.True(sample["available"]!.GetValue<bool>());
        Assert.Equal($"/api/device/software-packages/{apk.Id:D}/download", sample["downloadUrl"]!.GetValue<string>());
        Assert.Equal(apk.Sha256, sample["sha256"]!.GetValue<string>());
        Assert.Equal(7, sample["versionCode"]!.GetValue<long>());

        var missing = Assert.Single(apps, x => x["packageName"]!.GetValue<string>() == "com.honeywell.missing");
        Assert.False(missing["available"]!.GetValue<bool>());

        // AVAILABLE va como opcional (sin Play Store el agente la instala si hay APK);
        // el propio agente nunca se incluye.
        var chrome = Assert.Single(apps, x => x["packageName"]!.GetValue<string>() == "com.android.chrome");
        Assert.False(chrome["required"]!.GetValue<bool>());
        Assert.True(sample["required"]!.GetValue<bool>());
        Assert.DoesNotContain(apps, x => x["packageName"]!.GetValue<string>() == "com.titanmdm.agent");

        // La descarga del APK queda autorizada para ese equipo y no para otros.
        var deployment = new SoftwareDeploymentService(db, new DeviceCommandService(db, new NullNotifier()));
        var download = await deployment.GetPackageDownloadAsync(device.Id, apk.Id);
        Assert.NotNull(download);
        Assert.Equal("application/vnd.android.package-archive", download!.ContentType);
        Assert.Null(await deployment.GetPackageDownloadAsync(Guid.NewGuid(), apk.Id));
    }

    [Fact]
    public async Task Inscripcion_sin_politica_en_la_credencial_no_encola_nada()
    {
        await using var db = Open();
        var (organization, user) = await SeedOrganizationAsync(db);
        var created = await new EnrollmentService(db).CreateTokenAsync(
            organization.Id, user.Id, new CreateEnrollmentTokenRequest("Android", 60, 1));

        await CreateRegistrationService(db).RegisterAsync(NewRequest(created.Token, "CK65-0002"));

        Assert.Single(db.Devices);
        Assert.Empty(db.DeviceCommands);
    }

    [Fact]
    public async Task Planner_ignora_bloqueadas_y_al_agente()
    {
        var apps = AgentApplicationPlanner.ReadRequiredApplications(
            """
            {"applications":[
              {"packageName":"com.a.required","installType":"FORCE_INSTALLED"},
              {"packageName":"com.a.required","installType":"KIOSK"},
              {"packageName":"com.b.optional","installType":"AVAILABLE"},
              {"packageName":"com.c.blocked","installType":"BLOCKED"},
              {"packageName":"com.d.setup","installType":"REQUIRED_FOR_SETUP"},
              {"packageName":"com.titanmdm.agent","installType":"REQUIRED_FOR_SETUP"}
            ]}
            """);

        Assert.Equal(["com.a.required", "com.b.optional", "com.d.setup"], apps.Select(x => x.PackageName));
        Assert.Equal([true, false, true], apps.Select(x => x.Required));
        Assert.Empty(AgentApplicationPlanner.ReadRequiredApplications("no-json"));

        await using var db = Open();
        var none = await AgentApplicationPlanner.BuildAsync(db, Guid.NewGuid(), """{"applications":[]}""", CancellationToken.None);
        Assert.Empty(none);
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static RegisterDeviceRequest NewRequest(string token, string serial) =>
        new(token, "Honeywell CK65", "Android", serial, "Honeywell", "CK65", "Android", "11", "1.0.0", "172.21.20.50", null);

    private static async Task<(Organization Organization, User User)> SeedOrganizationAsync(TitanMdmDbContext db)
    {
        var organization = new Organization("CISA", $"cisa-{Guid.NewGuid():N}"[..12]);
        db.Organizations.Add(organization);
        var user = new User(organization.Id, "Admin", "TI", $"admin-{Guid.NewGuid():N}@cisa.test");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (organization, user);
    }

    private static async Task<Policy> SeedPolicyAsync(TitanMdmDbContext db, Guid organizationId, Guid userId, bool published)
    {
        var policy = new Policy(organizationId, "Handheld CEDI", null, PolicyPlatform.Android, userId);
        policy.SetCurrentVersion(1);
        policy.Activate();
        db.Policies.Add(policy);

        if (published)
        {
            var publication = new AndroidPolicyPublication(organizationId, policy.Id, Guid.NewGuid(), 1, $"titanmdm-{policy.Id:N}-v1");
            publication.BeginPublication(
                $$"""
                {
                  "applications": [
                    { "packageName": "{{SamplePackage}}", "installType": "FORCE_INSTALLED" },
                    { "packageName": "com.honeywell.missing", "installType": "FORCE_INSTALLED" },
                    { "packageName": "com.android.chrome", "installType": "AVAILABLE" },
                    {
                      "packageName": "com.titanmdm.agent",
                      "installType": "REQUIRED_FOR_SETUP",
                      "managedConfiguration": { "titan_launcher_enabled": true, "titan_company_name": "CISA" }
                    }
                  ]
                }
                """);
            db.AndroidPolicyPublications.Add(publication);
        }

        await db.SaveChangesAsync();
        return policy;
    }

    private static async Task<SoftwarePackage> SeedApkPackageAsync(TitanMdmDbContext db, Guid organizationId, Guid userId)
    {
        var id = Guid.NewGuid();
        var relative = Path.Combine(organizationId.ToString("N"), $"{id:N}.apk");
        var full = Path.Combine(SoftwarePackageStorage.Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.Copy(SampleApk, full, overwrite: true);

        await using var stream = File.OpenRead(full);
        var sha = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream));

        var package = new SoftwarePackage(
            organizationId, "Titan E2E Sample", "1.7", "APK", "titan-e2e-sample.apk",
            $"{id:N}.apk", relative, sha, new FileInfo(full).Length, null, userId, id);
        db.SoftwarePackages.Add(package);
        await db.SaveChangesAsync();
        return package;
    }

    private static DeviceRegistrationService CreateRegistrationService(TitanMdmDbContext db) =>
        new(
            db,
            new NullDispatcher(),
            new AndroidPolicyAssignmentService(
                db,
                new AndroidManagementClient(
                    new HttpClient(),
                    Options.Create(new AndroidManagementOptions { ProjectId = "titan-test", MaxRetryAttempts = 0 }),
                    new StubTokenProvider(),
                    NullLogger<AndroidManagementClient>.Instance),
                NullLogger<AndroidPolicyAssignmentService>.Instance,
                new DeviceCommandService(db, new NullNotifier())),
            NullLogger<DeviceRegistrationService>.Instance);

    private static TitanMdmDbContext Open() =>
        new(new DbContextOptionsBuilder<TitanMdmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private sealed class NullDispatcher : IAutomationEventDispatcher
    {
        public Task DispatchAsync(Guid organizationId, Guid? deviceId, string triggerType, object? payload = null,
            Guid? actorUserId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NullNotifier : IDeviceCommandNotifier
    {
        public Task NotifyAvailableAsync(Guid deviceId, Guid commandId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task NotifyUpdatedAsync(DeviceCommandDto command, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubTokenProvider : IGoogleAndroidAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult("test-token");

        public Task<bool> CanAuthenticateAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
