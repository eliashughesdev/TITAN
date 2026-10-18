using Microsoft.EntityFrameworkCore;

using TitanMDM.Application.Automation;
using TitanMDM.Application.Location;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Location;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.IntegrationTests;

public sealed class DeviceLocationServiceTests
{
    [Fact]
    public async Task OnDemandReport_IsAlwaysPersisted_EvenWhenPolicyDisabled()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        var service = CreateService(db);

        await service.ProcessLocationAsync(
            device.Id,
            """
            { "latitude": -34.6037, "longitude": -58.3816,
              "accuracyMeters": 12.5, "source": "OnDemand",
              "capturedAtUtc": "2026-10-18T00:00:00Z" }
            """);

        var location = Assert.Single(db.DeviceLocations);
        Assert.Equal(device.Id, location.DeviceId);
        Assert.Equal(-34.6037, location.Latitude);

        var state = Assert.Single(db.DeviceLocationStates);
        Assert.Equal(DeviceCaptureState.Reported, state.CaptureState);
    }

    [Fact]
    public async Task ProactiveReport_IsIgnored_WhenPolicyMissingOrDisabled()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        var service = CreateService(db);

        await service.ProcessLocationAsync(
            device.Id,
            """
            { "latitude": -34.6037, "longitude": -58.3816,
              "source": "AndroidAgentProactive",
              "capturedAtUtc": "2026-10-18T00:00:00Z" }
            """);

        Assert.Empty(db.DeviceLocations);

        db.LocationReportingPolicies.Add(
            new LocationReportingPolicy(
                device.OrganizationId,
                isEnabled: false,
                reportingIntervalMinutes: 15,
                retentionDays: 30));
        await db.SaveChangesAsync();

        await service.ProcessLocationAsync(
            device.Id,
            """
            { "latitude": -34.6037, "longitude": -58.3816,
              "source": "AndroidAgentProactive",
              "capturedAtUtc": "2026-10-18T00:00:00Z" }
            """);

        Assert.Empty(db.DeviceLocations);
    }

    [Fact]
    public async Task ProactiveReport_IsPersisted_WhenPolicyEnabled()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        db.LocationReportingPolicies.Add(
            new LocationReportingPolicy(
                device.OrganizationId,
                isEnabled: true,
                reportingIntervalMinutes: 15,
                retentionDays: 30));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.ProcessLocationAsync(
            device.Id,
            """
            { "latitude": -34.6037, "longitude": -58.3816,
              "source": "AndroidAgentProactive",
              "capturedAtUtc": "2026-10-18T00:00:00Z" }
            """);

        Assert.Single(db.DeviceLocations);
    }

    [Fact]
    public async Task PermissionDeniedReport_RecordsCaptureState_WithoutInventingCoordinates()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        var notifier = new StubNotifier();
        var service = CreateService(db, notifier);

        await service.ProcessLocationAsync(
            device.Id,
            """
            { "status": "PERMISSION_DENIED", "source": "AndroidAgent" }
            """);

        Assert.Empty(db.DeviceLocations);

        var state = Assert.Single(db.DeviceLocationStates);
        Assert.Equal(DeviceCaptureState.PermissionDenied, state.CaptureState);

        var latest = await service.GetLatestAsync(
            device.OrganizationId,
            device.Id);
        Assert.Null(latest);

        Assert.Single(notifier.Calls);
    }

    [Theory]
    [InlineData("UNAVAILABLE", DeviceCaptureState.Unavailable)]
    [InlineData("DISABLED", DeviceCaptureState.DisabledByPolicy)]
    public async Task StatusOnlyReport_MapsExpectedCaptureState(
        string status,
        DeviceCaptureState expected)
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        var service = CreateService(db);

        await service.ProcessLocationAsync(
            device.Id,
            $$"""{ "status": "{{status}}" }""");

        var state = Assert.Single(db.DeviceLocationStates);
        Assert.Equal(expected, state.CaptureState);
        Assert.Empty(db.DeviceLocations);
    }

    [Fact]
    public async Task StatusOnlyReport_UpsertsExistingState_InsteadOfDuplicating()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        var service = CreateService(db);

        await service.ProcessLocationAsync(
            device.Id,
            """{ "status": "PERMISSION_DENIED" }""");
        await service.ProcessLocationAsync(
            device.Id,
            """{ "status": "UNAVAILABLE" }""");

        var state = Assert.Single(db.DeviceLocationStates);
        Assert.Equal(DeviceCaptureState.Unavailable, state.CaptureState);
    }

    [Fact]
    public async Task ProactiveStatusReport_IsRecorded_EvenWhenPolicyDisabled()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        var service = CreateService(db);

        await service.ProcessLocationAsync(
            device.Id,
            """
            { "status": "PERMISSION_DENIED",
              "source": "AndroidAgentProactive" }
            """);

        var state = Assert.Single(db.DeviceLocationStates);
        Assert.Equal(DeviceCaptureState.PermissionDenied, state.CaptureState);
    }

    [Fact]
    public async Task GetLatestAndHistory_AreIsolatedByOrganization()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        var service = CreateService(db);

        await service.ProcessLocationAsync(
            device.Id,
            """
            { "latitude": -34.6037, "longitude": -58.3816,
              "source": "OnDemand",
              "capturedAtUtc": "2026-10-18T00:00:00Z" }
            """);

        var foreignOrganizationId = Guid.NewGuid();

        Assert.Null(
            await service.GetLatestAsync(
                foreignOrganizationId,
                device.Id));

        Assert.Empty(
            await service.GetHistoryAsync(
                foreignOrganizationId,
                device.Id,
                10));
    }

    [Fact]
    public async Task GetHistory_RespectsClampedLimit()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        var service = CreateService(db);

        for (var i = 0; i < 3; i++)
        {
            db.DeviceLocations.Add(
                new DeviceLocation(
                    device.OrganizationId,
                    device.Id,
                    -34.6,
                    -58.3,
                    10,
                    null,
                    null,
                    "OnDemand",
                    DateTime.UtcNow.AddMinutes(-i)));
        }

        await db.SaveChangesAsync();

        var history = await service.GetHistoryAsync(
            device.OrganizationId,
            device.Id,
            limit: 0);

        Assert.Single(history);
    }

    [Fact]
    public async Task FleetLocations_ResolveFreshStaleNoLocationAndPermissionDenied()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();

        var fresh = await SeedDeviceAsync(
            db,
            organizationId,
            "Fresh device");
        var stale = await SeedDeviceAsync(
            db,
            organizationId,
            "Stale device");
        var withoutLocation = await SeedDeviceAsync(
            db,
            organizationId,
            "No location device");
        var denied = await SeedDeviceAsync(
            db,
            organizationId,
            "Denied device");

        db.DeviceLocations.Add(
            new DeviceLocation(
                organizationId,
                fresh.Id,
                -34.6,
                -58.3,
                10,
                null,
                null,
                "OnDemand",
                DateTime.UtcNow.AddMinutes(-2)));

        db.DeviceLocations.Add(
            new DeviceLocation(
                organizationId,
                stale.Id,
                -34.7,
                -58.4,
                10,
                null,
                null,
                "OnDemand",
                DateTime.UtcNow.AddHours(-3)));

        db.DeviceLocationStates.Add(
            new DeviceLocationState(
                organizationId,
                denied.Id,
                DeviceCaptureState.PermissionDenied,
                DateTime.UtcNow,
                "AndroidAgent"));

        await db.SaveChangesAsync();

        var service = CreateService(db);
        var fleet = await service.GetFleetLocationsAsync(
            organizationId,
            new FleetLocationFilters(
                Search: null,
                Manufacturer: null,
                Department: null,
                DeviceGroupId: null,
                SiteId: null,
                Status: null,
                LocationState: null,
                MaxSignalAgeMinutes: null));

        Assert.Equal(4, fleet.Count);
        Assert.Equal(
            "Fresh",
            fleet.Single(x => x.DeviceId == fresh.Id).LocationState);
        Assert.Equal(
            "Stale",
            fleet.Single(x => x.DeviceId == stale.Id).LocationState);
        Assert.Equal(
            "NoLocation",
            fleet.Single(x => x.DeviceId == withoutLocation.Id).LocationState);
        Assert.Equal(
            "PermissionDenied",
            fleet.Single(x => x.DeviceId == denied.Id).LocationState);
    }

    [Fact]
    public async Task FleetLocations_AreIsolatedByOrganization()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var device = await SeedDeviceAsync(
            db,
            organizationId,
            "Org device");

        db.DeviceLocations.Add(
            new DeviceLocation(
                organizationId,
                device.Id,
                -34.6,
                -58.3,
                10,
                null,
                null,
                "OnDemand",
                DateTime.UtcNow));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        Assert.Empty(
            await service.GetFleetLocationsAsync(
                Guid.NewGuid(),
                EmptyFilters()));
        Assert.Single(
            await service.GetFleetLocationsAsync(
                organizationId,
                EmptyFilters()));
    }

    [Fact]
    public async Task FleetLocations_ExcludeNonAndroidDevices_FromMixedInventory()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var android = await SeedDeviceAsync(
            db,
            organizationId,
            "Android tablet",
            DevicePlatform.Android);
        var windows = await SeedDeviceAsync(
            db,
            organizationId,
            "Windows laptop",
            DevicePlatform.Windows);

        db.DeviceLocations.AddRange(
            new DeviceLocation(
                organizationId,
                android.Id,
                -34.6,
                -58.3,
                10,
                null,
                null,
                "AndroidAgent",
                DateTime.UtcNow),
            new DeviceLocation(
                organizationId,
                windows.Id,
                -34.7,
                -58.4,
                10,
                null,
                null,
                "WindowsAgent",
                DateTime.UtcNow));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var fleet = await service.GetFleetLocationsAsync(
            organizationId,
            EmptyFilters());

        Assert.Equal(
            android.Id,
            Assert.Single(fleet).DeviceId);
        Assert.All(
            fleet,
            row => Assert.Equal("Android", row.Platform));
        Assert.DoesNotContain(
            fleet,
            row => row.DeviceId == windows.Id);
    }

    [Fact]
    public async Task FleetLocations_ApplySearchAndLocationStateFilters()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var matching = await SeedDeviceAsync(
            db,
            organizationId,
            "Tablet-Alfa");
        var other = await SeedDeviceAsync(
            db,
            organizationId,
            "Tablet-Beta");

        db.DeviceLocations.Add(
            new DeviceLocation(
                organizationId,
                matching.Id,
                -34.6,
                -58.3,
                10,
                null,
                null,
                "OnDemand",
                DateTime.UtcNow));

        db.DeviceLocations.Add(
            new DeviceLocation(
                organizationId,
                other.Id,
                -34.7,
                -58.4,
                10,
                null,
                null,
                "OnDemand",
                DateTime.UtcNow.AddDays(-5)));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var searched = await service.GetFleetLocationsAsync(
            organizationId,
            EmptyFilters() with { Search = "Alfa" });
        Assert.Equal(
            matching.Id,
            Assert.Single(searched).DeviceId);

        var staleOnly = await service.GetFleetLocationsAsync(
            organizationId,
            EmptyFilters() with
            {
                LocationState = "Stale",
                MaxSignalAgeMinutes = 10080
            });
        Assert.Equal(
            other.Id,
            Assert.Single(staleOnly).DeviceId);
    }

    [Fact]
    public async Task UpdateReportingPolicy_CreatesThenUpdatesSingleRow()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var service = CreateService(db);

        Assert.Null(
            await service.GetReportingPolicyAsync(organizationId));

        var created = await service.UpdateReportingPolicyAsync(
            organizationId,
            new UpdateReportingPolicyRequest(
                IsEnabled: true,
                ReportingIntervalMinutes: 10,
                RetentionDays: 15));
        Assert.True(created.IsEnabled);

        var updated = await service.UpdateReportingPolicyAsync(
            organizationId,
            new UpdateReportingPolicyRequest(
                IsEnabled: false,
                ReportingIntervalMinutes: 20,
                RetentionDays: 45));

        var policy = Assert.Single(db.LocationReportingPolicies);
        Assert.Equal(policy.Id, updated.Id);
        Assert.False(policy.IsEnabled);
        Assert.Equal(20, policy.ReportingIntervalMinutes);
        Assert.Equal(45, policy.RetentionDays);
    }

    [Fact]
    public async Task PurgeOldLocations_RemovesOnlyPastRetentionWindow()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var service = CreateService(db);

        await service.UpdateReportingPolicyAsync(
            organizationId,
            new UpdateReportingPolicyRequest(
                IsEnabled: true,
                ReportingIntervalMinutes: 15,
                RetentionDays: 30));

        var recent = new DeviceLocation(
            organizationId,
            Guid.NewGuid(),
            -34.6,
            -58.3,
            10,
            null,
            null,
            "OnDemand",
            DateTime.UtcNow.AddDays(-5));

        var ancient = new DeviceLocation(
            organizationId,
            Guid.NewGuid(),
            -34.7,
            -58.4,
            10,
            null,
            null,
            "OnDemand",
            DateTime.UtcNow.AddDays(-60));

        db.DeviceLocations.AddRange(recent, ancient);
        await db.SaveChangesAsync();

        await service.PurgeOldLocationsAsync(organizationId);

        var remaining = Assert.Single(db.DeviceLocations);
        Assert.Equal(recent.Id, remaining.Id);
    }

    [Fact]
    public async Task PurgeOldLocations_IsSkipped_WhenPolicyDisabled()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var service = CreateService(db);

        var ancient = new DeviceLocation(
            organizationId,
            Guid.NewGuid(),
            -34.7,
            -58.4,
            10,
            null,
            null,
            "OnDemand",
            DateTime.UtcNow.AddDays(-400));

        db.DeviceLocations.Add(ancient);
        await db.SaveChangesAsync();

        await service.PurgeOldLocationsAsync(organizationId);

        Assert.Single(db.DeviceLocations);
    }

    [Fact]
    public async Task OfflineDeviceWithoutLocation_ResolvesNoLocation_BeforeOffline()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        device.MarkOffline();
        var service = CreateService(db);

        var fleet = await service.GetFleetLocationsAsync(
            device.OrganizationId,
            EmptyFilters());

        var row = fleet.Single(x => x.DeviceId == device.Id);
        Assert.Equal("NoLocation", row.LocationState);
        Assert.Null(row.Latitude);
        Assert.Null(row.Longitude);
        Assert.Null(row.SignalAgeMinutes);
    }

    [Fact]
    public async Task OfflineDevice_WithOldLocation_ResolvesOffline()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        device.MarkOffline();

        db.DeviceLocations.Add(
            new DeviceLocation(
                device.OrganizationId,
                device.Id,
                -34.6,
                -58.3,
                10,
                null,
                null,
                "OnDemand",
                DateTime.UtcNow));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var fleet = await service.GetFleetLocationsAsync(
            device.OrganizationId,
            EmptyFilters());

        Assert.Equal(
            "Offline",
            fleet.Single(x => x.DeviceId == device.Id).LocationState);
    }

    [Fact]
    public async Task GeofenceEnter_DispatchesAutomationEvent()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        var automation = new StubDispatcher();
        var service = CreateService(db, automation: automation);

        var geofence = new Geofence(
            device.OrganizationId,
            "Casa central",
            null,
            -34.6037,
            -58.3816,
            500,
            alertOnEnter: true,
            alertOnExit: true);
        db.Geofences.Add(geofence);

        db.GeofenceDeviceAssignments.Add(
            new GeofenceDeviceAssignment(
                device.OrganizationId,
                geofence.Id,
                device.Id));
        await db.SaveChangesAsync();

        await service.ProcessLocationAsync(
            device.Id,
            """
            { "latitude": -34.6038, "longitude": -58.3817,
              "source": "OnDemand",
              "capturedAtUtc": "2026-10-18T00:00:00Z" }
            """);

        var triggered = Assert.Single(automation.Triggers);
        Assert.Equal("GeofenceEnter", triggered);
        Assert.Single(db.GeofenceEvents);
        Assert.Single(db.GeofenceDeviceStates);
    }

    [Fact]
    public async Task InvalidCoordinates_AreRejected_WithoutPersisting()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessLocationAsync(
                device.Id,
                """{ "latitude": 120.0, "longitude": 0.0 }"""));

        Assert.Empty(db.DeviceLocations);
        Assert.Empty(db.DeviceLocationStates);
    }

    [Fact]
    public async Task UnknownDevice_IsRejected()
    {
        await using var db = Open();
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessLocationAsync(
                Guid.NewGuid(),
                """{ "latitude": 0.0, "longitude": 0.0 }"""));
    }

    private static DeviceLocationService CreateService(
        TitanMdmDbContext db,
        StubNotifier? notifier = null,
        StubDispatcher? automation = null)
    {
        return new DeviceLocationService(
            db,
            automation ?? new StubDispatcher(),
            notifier);
    }

    private static FleetLocationFilters EmptyFilters()
    {
        return new FleetLocationFilters(
            Search: null,
            Manufacturer: null,
            Department: null,
            DeviceGroupId: null,
            SiteId: null,
            Status: null,
            LocationState: null,
            MaxSignalAgeMinutes: null);
    }

    private static TitanMdmDbContext Open()
    {
        var options = new DbContextOptionsBuilder<TitanMdmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new TitanMdmDbContext(options);
    }

    private static async Task<Device> SeedDeviceAsync(
        TitanMdmDbContext db,
        Guid? organizationId = null,
        string deviceName = "Test device",
        DevicePlatform platform = DevicePlatform.Android)
    {
        var device = new Device(
            organizationId ?? Guid.NewGuid(),
            deviceName,
            platform,
            Guid.NewGuid().ToString("N"));
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return device;
    }

    private sealed class StubDispatcher
        : IAutomationEventDispatcher
    {
        public List<string> Triggers { get; } = [];

        public Task DispatchAsync(
            Guid organizationId,
            Guid? deviceId,
            string triggerType,
            object? payload = null,
            Guid? actorUserId = null,
            CancellationToken cancellationToken = default)
        {
            Triggers.Add(triggerType);
            return Task.CompletedTask;
        }
    }

    private sealed class StubNotifier
        : ILocationUpdateNotifier
    {
        public List<(Guid OrganizationId, Guid DeviceId, string LocationState)>
            Calls
        {
            get;
        } = [];

        public Task NotifyLocationUpdatedAsync(
            Guid organizationId,
            Guid deviceId,
            string locationState,
            DateTime? capturedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((organizationId, deviceId, locationState));
            return Task.CompletedTask;
        }
    }
}
