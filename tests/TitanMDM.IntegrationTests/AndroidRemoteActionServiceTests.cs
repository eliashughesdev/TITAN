using System.Net;
using System.Text;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using TitanMDM.Application.AndroidEnterprise;
using TitanMDM.Application.Audit;
using TitanMDM.Application.Security;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Android;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.IntegrationTests;

public sealed class AndroidRemoteActionServiceTests
{
    private const string GoogleDeviceName =
        "enterprises/LC000000/devices/device-1";

    [Fact]
    public async Task Lock_IssuesExpectedCommandAndWritesSuccessAudit()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        var handler = new StubHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            """
            { "name": "enterprises/LC000000/devices/device-1/operations/lock-1" }
            """));
        var auditWriter = new StubAuditWriter();
        var scope = new StubScopeAccessService(true);
        var service = CreateService(db, handler, auditWriter, scope);
        var userId = Guid.NewGuid();

        var result = await service.ExecuteAsync(
            device.OrganizationId,
            userId,
            device.Id,
            new AndroidRemoteActionRequest("LOCK"),
            "correlation-lock",
            "192.0.2.1");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            "/v1/enterprises/LC000000/devices/device-1:issueCommand",
            request.Path);
        Assert.Equal("LOCK", JsonNode.Parse(request.Body!)?["type"]?.GetValue<string>());
        Assert.Equal("Issued", result.Status);
        Assert.Equal(
            "enterprises/LC000000/devices/device-1/operations/lock-1",
            result.GoogleCommandName);
        Assert.Equal(
            (device.OrganizationId, userId, device.Id),
            Assert.Single(scope.DeviceChecks));

        var audit = Assert.Single(auditWriter.Entries);
        Assert.Equal("Android.RemoteAction.Lock", audit.Action);
        Assert.Equal("AndroidDevice", audit.TargetType);
        Assert.Equal(device.Id.ToString(), audit.TargetId);
        Assert.Equal("Success", audit.Result);
        Assert.Equal("correlation-lock", audit.CorrelationId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrCrossOrganizationDevice_IsHiddenWithoutHttp(
        bool seedInAnotherOrganization)
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        if (seedInAnotherOrganization)
        {
            var foreignDevice = new Device(
                Guid.NewGuid(),
                "Foreign Android",
                DevicePlatform.Android,
                "FOREIGN-1");
            db.Devices.Add(foreignDevice);
            await db.SaveChangesAsync();
            deviceId = foreignDevice.Id;
        }

        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "{}"));
        var service = CreateService(
            db,
            handler,
            new StubAuditWriter(),
            new StubScopeAccessService(false));

        var exception = await Assert.ThrowsAsync<AndroidRemoteActionException>(() =>
            service.ExecuteAsync(
                organizationId,
                Guid.NewGuid(),
                deviceId,
                new AndroidRemoteActionRequest("LOCK"),
                "correlation-hidden",
                null));

        Assert.Equal("DEVICE_NOT_FOUND", exception.Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task DeniedSiteScope_IsHiddenWithoutHttp()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "{}"));
        var scope = new StubScopeAccessService(false);
        var service = CreateService(db, handler, new StubAuditWriter(), scope);
        var userId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<AndroidRemoteActionException>(() =>
            service.ExecuteAsync(
                device.OrganizationId,
                userId,
                device.Id,
                new AndroidRemoteActionRequest("LOCK"),
                "correlation-scope",
                null));

        Assert.Equal("DEVICE_NOT_FOUND", exception.Code);
        Assert.Empty(handler.Requests);
        Assert.Equal(
            (device.OrganizationId, userId, device.Id),
            Assert.Single(scope.DeviceChecks));
    }

    [Fact]
    public async Task RecentSuccessfulAction_IsRejectedAsDuplicateWithoutHttp()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        db.AdministrativeAuditEvents.Add(
            new AdministrativeAuditEvent(
                device.OrganizationId,
                Guid.NewGuid(),
                "Android.RemoteAction.Lock",
                "AndroidDevice",
                device.Id.ToString(),
                "Success",
                "previous-correlation"));
        await db.SaveChangesAsync();
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "{}"));
        var auditWriter = new StubAuditWriter();
        var service = CreateService(
            db,
            handler,
            auditWriter,
            new StubScopeAccessService(true));

        var exception = await Assert.ThrowsAsync<AndroidRemoteActionException>(() =>
            service.ExecuteAsync(
                device.OrganizationId,
                Guid.NewGuid(),
                device.Id,
                new AndroidRemoteActionRequest("LOCK"),
                "correlation-duplicate",
                null));

        Assert.Equal("DUPLICATE_REMOTE_ACTION", exception.Code);
        Assert.Empty(handler.Requests);
        Assert.Empty(auditWriter.Entries);
    }

    [Fact]
    public async Task ResetPasswordWithoutPassword_PreservesValidationCodeWithoutHttp()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        db.AdministrativeAuditEvents.Add(
            new AdministrativeAuditEvent(
                device.OrganizationId,
                Guid.NewGuid(),
                "Android.RemoteAction.ResetPassword",
                "AndroidDevice",
                device.Id.ToString(),
                "Success",
                "previous-password-correlation"));
        await db.SaveChangesAsync();
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "{}"));
        var auditWriter = new StubAuditWriter();
        var service = CreateService(
            db,
            handler,
            auditWriter,
            new StubScopeAccessService(true));

        var exception = await Assert.ThrowsAsync<AndroidRemoteActionException>(() =>
            service.ExecuteAsync(
                device.OrganizationId,
                Guid.NewGuid(),
                device.Id,
                new AndroidRemoteActionRequest("RESET_PASSWORD"),
                "correlation-password",
                null));

        Assert.Equal("PASSWORD_REQUIRED", exception.Code);
        Assert.Empty(handler.Requests);
        Assert.Empty(auditWriter.Entries);
    }

    [Fact]
    public async Task ApiFailure_WritesFailureAuditAndReturnsSafeError()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db);
        var handler = new StubHandler(_ => JsonResponse(
            HttpStatusCode.BadRequest,
            """
            { "error": { "message": "sensitive upstream detail" } }
            """));
        var auditWriter = new StubAuditWriter();
        var service = CreateService(
            db,
            handler,
            auditWriter,
            new StubScopeAccessService(true));

        var exception = await Assert.ThrowsAsync<AndroidRemoteActionException>(() =>
            service.ExecuteAsync(
                device.OrganizationId,
                Guid.NewGuid(),
                device.Id,
                new AndroidRemoteActionRequest("LOCK"),
                "correlation-failure",
                null));

        Assert.Equal("ANDROID_REMOTE_ACTION_FAILED", exception.Code);
        Assert.DoesNotContain("sensitive upstream detail", exception.Message);
        Assert.Single(handler.Requests);
        Assert.Equal("Failure", Assert.Single(auditWriter.Entries).Result);
    }

    [Fact]
    public async Task NonAndroidDevice_IsRejectedWithoutHttp()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(
            db,
            DevicePlatform.Windows,
            enrolled: false);
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "{}"));
        var service = CreateService(
            db,
            handler,
            new StubAuditWriter(),
            new StubScopeAccessService(true));

        var exception = await Assert.ThrowsAsync<AndroidRemoteActionException>(() =>
            service.ExecuteAsync(
                device.OrganizationId,
                Guid.NewGuid(),
                device.Id,
                new AndroidRemoteActionRequest("LOCK"),
                "correlation-platform",
                null));

        Assert.Equal("NOT_ANDROID_DEVICE", exception.Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UnenrolledAndroidDevice_IsRejectedWithoutHttp()
    {
        await using var db = Open();
        var device = await SeedDeviceAsync(db, enrolled: false);
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "{}"));
        var service = CreateService(
            db,
            handler,
            new StubAuditWriter(),
            new StubScopeAccessService(true));

        var exception = await Assert.ThrowsAsync<AndroidRemoteActionException>(() =>
            service.ExecuteAsync(
                device.OrganizationId,
                Guid.NewGuid(),
                device.Id,
                new AndroidRemoteActionRequest("LOCK"),
                "correlation-enrollment",
                null));

        Assert.Equal("ANDROID_DEVICE_NOT_ENROLLED", exception.Code);
        Assert.Empty(handler.Requests);
    }

    private static AndroidRemoteActionService CreateService(
        TitanMdmDbContext db,
        StubHandler handler,
        StubAuditWriter auditWriter,
        StubScopeAccessService scopeAccess)
    {
        var options = new AndroidManagementOptions
        {
            ProjectId = "titan-test",
            MaxRetryAttempts = 0,
            RemoteActionDeduplicationSeconds = 30
        };
        var client = new AndroidManagementClient(
            new HttpClient(handler),
            Options.Create(options),
            new StubTokenProvider(),
            NullLogger<AndroidManagementClient>.Instance);

        return new AndroidRemoteActionService(
            db,
            client,
            auditWriter,
            scopeAccess,
            Options.Create(options),
            NullLogger<AndroidRemoteActionService>.Instance);
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
        DevicePlatform platform = DevicePlatform.Android,
        bool enrolled = true)
    {
        var device = new Device(
            Guid.NewGuid(),
            "Test device",
            platform,
            Guid.NewGuid().ToString("N"));
        db.Devices.Add(device);

        if (enrolled)
        {
            db.AndroidDevices.Add(
                new AndroidDevice(
                    device.OrganizationId,
                    device.Id,
                    GoogleDeviceName));
        }

        await db.SaveChangesAsync();
        return device;
    }

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(
                new CapturedRequest(
                    request.Method,
                    request.RequestUri!.AbsolutePath,
                    body));
            return responseFactory(request);
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        string Path,
        string? Body);

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

    private sealed class StubAuditWriter
        : IAdministrativeAuditWriter
    {
        public List<AdministrativeAuditEntry> Entries { get; } = [];

        public Task WriteAsync(
            AdministrativeAuditEntry entry,
            CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class StubScopeAccessService(bool canAccessDevice)
        : IScopeAccessService
    {
        public List<(Guid OrganizationId, Guid UserId, Guid DeviceId)> DeviceChecks
        {
            get;
        } = [];

        public Task<bool> HasOrganizationScopeAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(canAccessDevice);
        }

        public Task<IReadOnlyCollection<Guid>> GetAccessibleSiteIdsAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<Guid>>([]);
        }

        public Task<AuthorizationScopeSnapshot> GetScopeSnapshotAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                canAccessDevice
                    ? AuthorizationScopeSnapshot.Organization()
                    : AuthorizationScopeSnapshot.Empty());
        }

        public Task<bool> CanAccessSiteAsync(
            Guid organizationId,
            Guid userId,
            Guid siteId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(canAccessDevice);
        }

        public Task<bool> CanAccessDeviceAsync(
            Guid organizationId,
            Guid userId,
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            DeviceChecks.Add((organizationId, userId, deviceId));
            return Task.FromResult(canAccessDevice);
        }

        public Task<bool> CanAccessTicketAsync(
            Guid organizationId,
            Guid userId,
            Guid ticketId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(canAccessDevice);
        }
    }
}
