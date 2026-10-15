using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using TitanMDM.Application.AndroidEnterprise;
using TitanMDM.Application.Android.Policies;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Android;
using TitanMDM.Infrastructure.Android.Policies;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.IntegrationTests;

/*
 * ============================================================
 * TITANMDM ANDROID ENTERPRISE — CONSULTAS DE CONSOLA
 * ============================================================
 *
 * El inventario y la auditoría del Titan Launcher siempre deben
 * paginarse en servidor y respetar el aislamiento por
 * organización. Ninguna consulta puede exponer dispositivos de
 * otra organización ni eventos no relacionados con Android.
 */

public sealed class AndroidEnterpriseConsoleQueryTests
{
    private static readonly Guid UserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Inventory_IsIsolatedByOrganization_AndPaginated()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();

        await SeedAndroidDeviceAsync(db, organizationId, "Android A");
        await SeedAndroidDeviceAsync(db, organizationId, "Android B");
        await SeedAndroidDeviceAsync(db, organizationId, "Android C");
        await SeedAndroidDeviceAsync(db, otherOrganizationId, "Foreign");

        var service = CreateSyncService(db);

        var firstPage =
            await service.GetInventoryPageAsync(
                organizationId,
                null,
                1,
                2);

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.PageSize);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(2, firstPage.Items.Count);

        var secondPage =
            await service.GetInventoryPageAsync(
                organizationId,
                null,
                2,
                2);

        Assert.Single(secondPage.Items);

        var foreignPage =
            await CreateSyncService(db)
                .GetInventoryPageAsync(
                    otherOrganizationId,
                    null,
                    1,
                    50);

        Assert.Equal(1, foreignPage.TotalCount);
        Assert.Equal("Foreign", foreignPage.Items.Single().DeviceName);
    }

    [Fact]
    public async Task Inventory_SearchFiltersByNameOrAppliedPolicy()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();

        await SeedAndroidDeviceAsync(
            db,
            organizationId,
            "Caja principal",
            appliedPolicyName: "enterprises/e/policies/retail");

        await SeedAndroidDeviceAsync(
            db,
            organizationId,
            "Almacen",
            appliedPolicyName: "enterprises/e/policies/warehouse");

        var service = CreateSyncService(db);

        var byName =
            await service.GetInventoryPageAsync(
                organizationId,
                "principal",
                1,
                50);

        Assert.Equal("Caja principal", byName.Items.Single().DeviceName);

        var byPolicy =
            await service.GetInventoryPageAsync(
                organizationId,
                "warehouse",
                1,
                50);

        Assert.Equal("Almacen", byPolicy.Items.Single().DeviceName);
        Assert.Equal(
            "enterprises/e/policies/warehouse",
            byPolicy.Items.Single().AppliedPolicyName);
    }

    [Fact]
    public async Task Inventory_IsScopedByAccessibleSiteIds()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();

        await SeedAndroidDeviceAsync(
            db,
            organizationId,
            "Sitio A",
            siteId: siteA);

        await SeedAndroidDeviceAsync(
            db,
            organizationId,
            "Sitio B",
            siteId: siteB);

        await SeedAndroidDeviceAsync(
            db,
            organizationId,
            "Sin sitio");

        var service = CreateSyncService(db);

        var organizationWide =
            await service.GetInventoryPageAsync(
                organizationId,
                null,
                1,
                50,
                accessibleSiteIds: null);

        Assert.Equal(3, organizationWide.TotalCount);

        var scoped =
            await service.GetInventoryPageAsync(
                organizationId,
                null,
                1,
                50,
                accessibleSiteIds: new[] { siteA });

        Assert.Equal(1, scoped.TotalCount);
        Assert.Equal("Sitio A", scoped.Items.Single().DeviceName);
    }

    [Fact]
    public async Task Summary_IsScopedByAccessibleSiteIds()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();

        await SeedAndroidDeviceAsync(
            db,
            organizationId,
            "Sitio A",
            siteId: siteA);

        await SeedAndroidDeviceAsync(
            db,
            organizationId,
            "Sitio B",
            siteId: siteB);

        var service = CreateSyncService(db);

        var organizationWide =
            await service.GetSummaryAsync(
                organizationId,
                accessibleSiteIds: null);

        Assert.Equal(2, organizationWide.Total);

        var scoped =
            await service.GetSummaryAsync(
                organizationId,
                accessibleSiteIds: new[] { siteA });

        Assert.Equal(1, scoped.Total);
    }

    [Fact]
    public async Task AssignmentPage_ReturnsFleetWithAssignmentStateAndScope()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var siteA = Guid.NewGuid();

        await SeedAndroidDeviceAsync(
            db,
            organizationId,
            "Caja A",
            siteId: siteA);

        await SeedAndroidDeviceAsync(
            db,
            organizationId,
            "Sitio B");

        await SeedAndroidDeviceAsync(
            db,
            organizationId,
            "Sitio C");

        await SeedAndroidDeviceAsync(
            db,
            otherOrganizationId,
            "Ajeno");

        var firstDeviceId =
            await db.Devices
                .Where(d => d.OrganizationId == organizationId)
                .OrderBy(d => d.DeviceName)
                .Select(d => d.Id)
                .FirstAsync();

        db.DevicePolicyAssignments.Add(
            new DevicePolicyAssignment(
                organizationId,
                policyId,
                firstDeviceId,
                4));

        await db.SaveChangesAsync();

        var service =
            CreateAssignmentService(db);

        var page =
            await service.GetPageAsync(
                organizationId,
                policyId,
                null,
                accessibleSiteIds: null,
                1,
                2);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(2, page.Items.Count);
        Assert.Contains(
            page.Items,
            item => item.AssignmentStatus == "Pending");

        var unassigned =
            page.Items.Single(
                item =>
                    item.AssignmentStatus ==
                        "NotAssigned");

        Assert.Null(unassigned.AssignmentId);

        var siteScoped =
            await service.GetPageAsync(
                organizationId,
                policyId,
                null,
                accessibleSiteIds:
                    new[] { Guid.NewGuid() });

        Assert.Equal(0, siteScoped.TotalCount);
    }

    [Fact]
    public async Task Audit_ReturnsOnlyAndroidActionsForOrganization_WithActor()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();

        var user =
            new User(
                organizationId,
                "Ana",
                "Perez",
                "ana@example.com");

        db.Users.Add(user);

        db.AdministrativeAuditEvents.AddRange(
            new AdministrativeAuditEvent(
                organizationId,
                user.Id,
                "Android.RemoteAction.Lock",
                "AndroidDevice",
                Guid.NewGuid().ToString(),
                "Success",
                "correlation-android"),

            new AdministrativeAuditEvent(
                organizationId,
                user.Id,
                "RBAC.AndroidPolicies.Publish",
                "AndroidPolicies",
                null,
                "Success",
                "correlation-rbac"),

            new AdministrativeAuditEvent(
                organizationId,
                user.Id,
                "RBAC.Users.Update",
                "Users",
                null,
                "Success",
                "correlation-users"),

            new AdministrativeAuditEvent(
                otherOrganizationId,
                null,
                "Android.RemoteAction.Lock",
                "AndroidDevice",
                null,
                "Success",
                "correlation-foreign"));

        await db.SaveChangesAsync();

        var service = CreateEnterpriseService(db);

        var page =
            await service.GetAuditPageAsync(
                organizationId,
                null,
                1,
                50);

        Assert.Equal(2, page.TotalCount);
        Assert.Contains(
            page.Items,
            item =>
                item.Action ==
                "Android.RemoteAction.Lock");
        Assert.Contains(
            page.Items,
            item =>
                item.Action ==
                "RBAC.AndroidPolicies.Publish");
        Assert.DoesNotContain(
            page.Items,
            item =>
                item.Action ==
                "RBAC.Users.Update");

        var remoteAction =
            page.Items.Single(
                item =>
                    item.Action ==
                    "Android.RemoteAction.Lock");

        Assert.Equal("Ana Perez", remoteAction.ActorName);
        Assert.Equal("ana@example.com", remoteAction.ActorEmail);
        Assert.Equal(
            "correlation-android",
            remoteAction.CorrelationId);
    }

    [Fact]
    public async Task Audit_SearchAndPaging_AreServerSide()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();

        for (var index = 0; index < 3; index++)
        {
            db.AdministrativeAuditEvents.Add(
                new AdministrativeAuditEvent(
                    organizationId,
                    UserId,
                    index == 1
                        ? "Android.RemoteAction.Wipe"
                        : "Android.RemoteAction.Lock",
                    "AndroidDevice",
                    null,
                    "Success",
                    $"correlation-{index}"));
        }

        await db.SaveChangesAsync();

        var service = CreateEnterpriseService(db);

        var paged =
            await service.GetAuditPageAsync(
                organizationId,
                null,
                2,
                1);

        Assert.Equal(3, paged.TotalCount);
        Assert.Equal(3, paged.TotalPages);
        Assert.Single(paged.Items);

        var searched =
            await service.GetAuditPageAsync(
                organizationId,
                "Wipe",
                1,
                50);

        Assert.Single(searched.Items);
        Assert.Equal(
            "Android.RemoteAction.Wipe",
            searched.Items.Single().Action);
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static async Task SeedAndroidDeviceAsync(
        TitanMdmDbContext db,
        Guid organizationId,
        string deviceName,
        string? appliedPolicyName = null,
        Guid? siteId = null)
    {
        var device =
            new Device(
                organizationId,
                deviceName,
                DevicePlatform.Android,
                Guid.NewGuid().ToString("N"));

        if (siteId is Guid assignedSiteId)
        {
            device.AssignSite(assignedSiteId);
        }

        db.Devices.Add(device);

        var android =
            new AndroidDevice(
                organizationId,
                device.Id,
                $"enterprises/LC000000/devices/{device.Id:N}");

        android.Synchronize(
            googleDeviceId: device.Id.ToString("N"),
            managementMode: "FULLY_MANAGED",
            ownership: "OWNED",
            state: "ACTIVE",
            appliedPolicyName: appliedPolicyName,
            appliedPolicyVersion: 3,
            appliedPolicyState: "APPLIED",
            enrollmentTokenName: null,
            userName: null,
            brand: null,
            hardware: null,
            deviceBasebandVersion: null,
            bootloaderVersion: null,
            securityPatchLevel: null,
            apiLevel: null,
            buildNumber: null,
            kernelVersion: null,
            androidDevicePolicyVersion: null,
            androidDevicePolicyVersionCode: null,
            encryptionStatus: null,
            securityPosture: null,
            enrollmentTimeUtc: null,
            lastStatusReportTimeUtc: null,
            lastPolicySyncTimeUtc: DateTime.UtcNow);

        db.AndroidDevices.Add(android);

        await db.SaveChangesAsync();
    }

    private static AndroidDeviceSyncService CreateSyncService(
        TitanMdmDbContext db)
    {
        return new AndroidDeviceSyncService(
            db,
            CreateClient(),
            NullLogger<AndroidDeviceSyncService>.Instance);
    }

    private static AndroidPolicyAssignmentService CreateAssignmentService(
        TitanMdmDbContext db)
    {
        return new AndroidPolicyAssignmentService(
            db,
            CreateClient(),
            NullLogger<AndroidPolicyAssignmentService>.Instance);
    }

    private static AndroidEnterpriseService CreateEnterpriseService(
        TitanMdmDbContext db)
    {
        var options =
            new AndroidManagementOptions
            {
                ProjectId = "titan-test",
                MaxRetryAttempts = 0
            };

        return new AndroidEnterpriseService(
            db,
            CreateClient(options),
            new StubTokenProvider(),
            Options.Create(options));
    }

    private static AndroidManagementClient CreateClient(
        AndroidManagementOptions? options = null)
    {
        options ??=
            new AndroidManagementOptions
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

    private static TitanMdmDbContext Open()
    {
        var options =
            new DbContextOptionsBuilder<TitanMdmDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;

        return new TitanMdmDbContext(options);
    }

    private sealed class StubTokenProvider
        : IGoogleAndroidAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult("stub-token");
        }

        public Task<bool> CanAuthenticateAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
