using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using TitanMDM.Application.AndroidEnterprise;
using TitanMDM.Application.Commands;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Android;
using TitanMDM.Infrastructure.Android.Policies;
using TitanMDM.Infrastructure.Commands;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.IntegrationTests;

/*
 * Equipos Android inscritos por TitanMDM Agent (Kotlin), sin Android
 * Enterprise: no tienen fila en AndroidDevices y deben aparecer en el
 * Centro de Control y en los despliegues del Titan Launcher.
 */
public sealed class AndroidAgentFleetTests
{
    private static readonly Guid UserId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task InventoryAndSummary_IncludeAgentDevices()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();

        var agentDevice = await SeedAgentDeviceAsync(db, organizationId, "Honeywell CK65");
        await SeedAgentDeviceAsync(db, Guid.NewGuid(), "Ajeno");

        db.Devices.Add(new Device(organizationId, "PC Windows", DevicePlatform.Windows, "WIN-1"));
        await db.SaveChangesAsync();

        var service = CreateSyncService(db);

        var page = await service.GetInventoryPageAsync(organizationId, null, 1, 25);
        var item = Assert.Single(page.Items);
        Assert.Equal(agentDevice.Id, item.DeviceId);
        Assert.Equal(AndroidDeviceSyncService.TitanAgentManagementMode, item.ManagementMode);
        Assert.False(item.IsDeletedInGoogle);

        var bySerial = await service.GetInventoryPageAsync(organizationId, "0c2905d2", 1, 25);
        Assert.Single(bySerial.Items);

        var summary = await service.GetSummaryAsync(organizationId);
        Assert.Equal(1, summary.Total);
        Assert.Equal(1, summary.Managed);
        Assert.Equal(0, summary.MissingInGoogle);
    }

    [Fact]
    public async Task AssignmentPage_ListsAgentDevice()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        await SeedAgentDeviceAsync(db, organizationId, "Honeywell CK65");

        var page = await CreateAssignmentService(db)
            .GetPageAsync(organizationId, Guid.NewGuid(), null);

        var row = Assert.Single(page.Items);
        Assert.Equal("NotAssigned", row.AssignmentStatus);
        Assert.True(row.IsManaged);
    }

    [Fact]
    public async Task Assign_ToAgentDevice_QueuesLauncherCommandInSql()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var device = await SeedAgentDeviceAsync(db, organizationId, "Honeywell CK65");
        var policy = await SeedPublishedLauncherPolicyAsync(db, organizationId);

        var result = await CreateAssignmentService(db).AssignAsync(
            organizationId,
            policy.Id,
            device.Id,
            accessibleSiteIds: null,
            CancellationToken.None,
            UserId);

        Assert.Equal("Queued", result.AssignmentStatus);
        Assert.Equal("Pending", result.LauncherSyncStatus);

        var command = await db.DeviceCommands.SingleAsync();
        Assert.Equal(device.Id, command.DeviceId);
        Assert.Equal(AndroidPolicyAssignmentService.AgentLauncherCommandType, command.CommandType);

        var payload = JsonNode.Parse(command.PayloadJson)!.AsObject();
        Assert.Equal(policy.CurrentVersion, payload["policyVersion"]!.GetValue<int>());
        Assert.Equal("CISA", payload["launcher"]!["titan_company_name"]!.GetValue<string>());

        var assignment = await db.DevicePolicyAssignments.SingleAsync();
        Assert.Equal(command.Id, assignment.CommandId);

        var row = Assert.Single(
            (await CreateAssignmentService(db).GetPageAsync(organizationId, policy.Id, null)).Items);
        Assert.Equal("Queued", row.AssignmentStatus);
    }

    [Fact]
    public async Task Assign_ToAgentDevice_RequiresPublishedVersion()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var device = await SeedAgentDeviceAsync(db, organizationId, "Honeywell CK65");

        var policy = new Policy(organizationId, "Sin publicar", null, PolicyPlatform.Android, UserId);
        policy.SetCurrentVersion(1);
        db.Policies.Add(policy);
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateAssignmentService(db).AssignAsync(
                organizationId, policy.Id, device.Id, null, CancellationToken.None, UserId));

        Assert.Contains("Publique", error.Message);
        Assert.Empty(db.DeviceCommands);
    }

    [Fact]
    public void ExtractAgentLauncherConfiguration_ReturnsNullWithoutLauncher()
    {
        Assert.Null(AndroidPolicyAssignmentService.ExtractAgentLauncherConfiguration(
            """{"cameraAccess":"CAMERA_ACCESS_DISABLED"}"""));
        Assert.Null(AndroidPolicyAssignmentService.ExtractAgentLauncherConfiguration("no-json"));
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static async Task<Device> SeedAgentDeviceAsync(
        TitanMdmDbContext db,
        Guid organizationId,
        string name)
    {
        var device = new Device(
            organizationId,
            name,
            DevicePlatform.Android,
            name == "Ajeno" ? Guid.NewGuid().ToString("N") : "0c2905d2216cb2f4");

        device.CompleteEnrollment();
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return device;
    }

    private static async Task<Policy> SeedPublishedLauncherPolicyAsync(
        TitanMdmDbContext db,
        Guid organizationId)
    {
        var policy = new Policy(organizationId, "Launcher CEDI", null, PolicyPlatform.Android, UserId);
        policy.SetCurrentVersion(2);
        policy.Activate();
        db.Policies.Add(policy);

        var publication = new AndroidPolicyPublication(
            organizationId,
            policy.Id,
            Guid.NewGuid(),
            2,
            $"titanmdm-{policy.Id:N}-v2");

        publication.BeginPublication(
            """
            {
              "applications": [
                { "packageName": "com.android.chrome", "installType": "AVAILABLE" },
                {
                  "packageName": "com.titanmdm.agent",
                  "installType": "REQUIRED_FOR_SETUP",
                  "managedConfiguration": {
                    "titan_launcher_enabled": true,
                    "titan_company_name": "CISA",
                    "titan_config_version": 2
                  }
                }
              ]
            }
            """);

        db.AndroidPolicyPublications.Add(publication);
        await db.SaveChangesAsync();
        return policy;
    }

    private static AndroidDeviceSyncService CreateSyncService(TitanMdmDbContext db) =>
        new(db, CreateClient(), NullLogger<AndroidDeviceSyncService>.Instance);

    private static AndroidPolicyAssignmentService CreateAssignmentService(TitanMdmDbContext db) =>
        new(
            db,
            CreateClient(),
            NullLogger<AndroidPolicyAssignmentService>.Instance,
            new DeviceCommandService(db, new NullNotifier()));

    private static AndroidManagementClient CreateClient()
    {
        var options = new AndroidManagementOptions
        {
            ProjectId = "titan-test",
            MaxRetryAttempts = 0
        };

        return new AndroidManagementClient(
            new HttpClient(),
            Options.Create(options),
            new StubTokenProvider(),
            NullLogger<AndroidManagementClient>.Instance);
    }

    private static TitanMdmDbContext Open() =>
        new(new DbContextOptionsBuilder<TitanMdmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private sealed class NullNotifier : IDeviceCommandNotifier
    {
        public Task NotifyAvailableAsync(Guid deviceId, Guid commandId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task NotifyUpdatedAsync(DeviceCommandDto command, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubTokenProvider : IGoogleAndroidAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("test-token");

        public Task<bool> CanAuthenticateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
