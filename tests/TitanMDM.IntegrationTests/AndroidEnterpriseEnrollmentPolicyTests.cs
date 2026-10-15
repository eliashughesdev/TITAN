using System.Net;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using TitanMDM.Application.AndroidEnterprise;
using TitanMDM.Domain.Entities;
using TitanMDM.Domain.Enums;
using TitanMDM.Infrastructure.Android;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.IntegrationTests;

/*
 * ============================================================
 * TITANMDM ANDROID ENTERPRISE — VALIDACIÓN DE POLÍTICA
 * ============================================================
 *
 * Un enrollment token NUNCA debe emitirse para una política
 * inexistente, de otra organización, de otra plataforma o cuya
 * versión actual no esté publicada en Android Enterprise.
 */

public sealed class AndroidEnterpriseEnrollmentPolicyTests
{
    private static readonly Guid UserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task PolicyFromOtherOrganization_IsRejected()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();

        await SeedActiveConfigurationAsync(db, organizationId);

        var otherPolicy =
            new Policy(
                Guid.NewGuid(),
                "Otra",
                null,
                PolicyPlatform.Android,
                UserId);

        db.Policies.Add(otherPolicy);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateEnrollmentAsync(
                    organizationId,
                    UserId,
                    new CreateAndroidEnrollmentRequest(
                        "FullyManaged",
                        60,
                        otherPolicy.Id)));

        Assert.Contains(
            "no existe en la organización",
            exception.Message);
    }

    [Fact]
    public async Task PolicyFromAnotherPlatform_IsRejected()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();

        await SeedActiveConfigurationAsync(db, organizationId);

        var windowsPolicy =
            new Policy(
                organizationId,
                "Windows",
                null,
                PolicyPlatform.Windows,
                UserId);

        db.Policies.Add(windowsPolicy);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateEnrollmentAsync(
                    organizationId,
                    UserId,
                    new CreateAndroidEnrollmentRequest(
                        "FullyManaged",
                        60,
                        windowsPolicy.Id)));

        Assert.Contains(
            "no pertenece a Android",
            exception.Message);
    }

    [Fact]
    public async Task PolicyWithoutPublication_IsRejected()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();

        await SeedActiveConfigurationAsync(db, organizationId);

        var policy =
            new Policy(
                organizationId,
                "Android",
                null,
                PolicyPlatform.Android,
                UserId);

        db.Policies.Add(policy);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateEnrollmentAsync(
                    organizationId,
                    UserId,
                    new CreateAndroidEnrollmentRequest(
                        "FullyManaged",
                        60,
                        policy.Id)));

        Assert.Contains(
            "todavía no ha sido publicada",
            exception.Message);
    }

    [Fact]
    public async Task PolicyWithUnpublishedVersion_IsRejected()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();

        await SeedActiveConfigurationAsync(db, organizationId);

        var policy =
            new Policy(
                organizationId,
                "Android",
                null,
                PolicyPlatform.Android,
                UserId);

        db.Policies.Add(policy);

        var publication =
            new AndroidPolicyPublication(
                organizationId,
                policy.Id,
                Guid.NewGuid(),
                policy.CurrentVersion,
                policy.Id.ToString());

        db.AndroidPolicyPublications.Add(publication);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateEnrollmentAsync(
                    organizationId,
                    UserId,
                    new CreateAndroidEnrollmentRequest(
                        "FullyManaged",
                        60,
                        policy.Id)));

        Assert.Contains(
            "no está disponible",
            exception.Message);
    }

    [Fact]
    public async Task PublishedPolicy_AllowsEnrollment()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();

        await SeedActiveConfigurationAsync(db, organizationId);

        var policy =
            await SeedPublishedPolicyAsync(
                db,
                organizationId);

        var handler =
            new StubHandler();

        var service =
            CreateService(
                db,
                handler);

        var result =
            await service.CreateEnrollmentAsync(
                organizationId,
                UserId,
                new CreateAndroidEnrollmentRequest(
                    "FullyManaged",
                    60,
                    policy.Id));

        Assert.Equal(
            policy.Id,
            result.PolicyId);

        Assert.False(
            string.IsNullOrWhiteSpace(
                result.QrCode));

        Assert.Contains(
            $"enterprises/LC000000/policies/{policy.Id}",
            handler.LastRequestBody ?? string.Empty);

        Assert.Equal(
            1,
            await db.AndroidEnrollments.CountAsync());
    }

    [Fact]
    public async Task FullyManagedWithoutPolicy_IsRejected()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();

        await SeedActiveConfigurationAsync(db, organizationId);

        var service = CreateService(db);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateEnrollmentAsync(
                    organizationId,
                    UserId,
                    new CreateAndroidEnrollmentRequest(
                        "FullyManaged",
                        60)));

        Assert.Contains(
            "política Android publicada",
            exception.Message);
    }

    [Fact]
    public async Task WorkProfileWithoutPolicy_IsAllowed()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();

        await SeedActiveConfigurationAsync(db, organizationId);

        var service = CreateService(db);

        var result =
            await service.CreateEnrollmentAsync(
                organizationId,
                UserId,
                new CreateAndroidEnrollmentRequest(
                    "WorkProfile",
                    60));

        Assert.Null(result.PolicyId);
    }

    [Fact]
    public async Task PolicyIdNonexistent_IsRejected()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();

        await SeedActiveConfigurationAsync(db, organizationId);

        var service = CreateService(db);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateEnrollmentAsync(
                    organizationId,
                    UserId,
                    new CreateAndroidEnrollmentRequest(
                        "FullyManaged",
                        60,
                        Guid.NewGuid())));

        Assert.Contains(
            "no existe en la organización",
            exception.Message);

        Assert.Equal(
            0,
            await db.AndroidEnrollments.CountAsync());
    }

    [Fact]
    public async Task PublishedPolicyFromAnotherEnterprise_IsRejected()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();

        await SeedActiveConfigurationAsync(
            db,
            organizationId,
            "enterprises/LC000000");

        var policy =
            new Policy(
                organizationId,
                "Android",
                null,
                PolicyPlatform.Android,
                UserId);

        db.Policies.Add(policy);

        /*
         * La publicación apunta a una política perteneciente a
         * otra empresa Android Enterprise, no a
         * enterprises/LC000000.
         */
        var publication =
            new AndroidPolicyPublication(
                organizationId,
                policy.Id,
                Guid.NewGuid(),
                policy.CurrentVersion,
                policy.Id.ToString());

        publication.BeginPublication("{}");
        publication.MarkPublished(
            "enterprises/LC999999/policies/otra",
            "{}");

        db.AndroidPolicyPublications.Add(publication);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateEnrollmentAsync(
                    organizationId,
                    UserId,
                    new CreateAndroidEnrollmentRequest(
                        "FullyManaged",
                        60,
                        policy.Id)));

        Assert.Contains(
            "no pertenece a la empresa Android Enterprise activa",
            exception.Message);

        Assert.Equal(
            0,
            await db.AndroidEnrollments.CountAsync());
    }

    [Fact]
    public async Task GoogleError_PropagatesAndDoesNotPersistEnrollment()
    {
        await using var db = Open();
        var organizationId = Guid.NewGuid();

        await SeedActiveConfigurationAsync(db, organizationId);

        var policy =
            await SeedPublishedPolicyAsync(
                db,
                organizationId);

        var service =
            CreateService(db, new FailingHandler());

        await Assert.ThrowsAsync<AndroidManagementApiException>(
            () => service.CreateEnrollmentAsync(
                organizationId,
                UserId,
                new CreateAndroidEnrollmentRequest(
                    "FullyManaged",
                    60,
                    policy.Id)));

        Assert.Equal(
            0,
            await db.AndroidEnrollments.CountAsync());
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static async Task SeedActiveConfigurationAsync(
        TitanMdmDbContext db,
        Guid organizationId,
        string enterpriseName = "enterprises/LC000000")
    {
        var configuration =
            new AndroidEnterpriseConfiguration(
                organizationId,
                "titan-test");

        configuration.Activate(
            enterpriseName,
            "Titan Test");

        db.AndroidEnterpriseConfigurations.Add(
            configuration);

        await db.SaveChangesAsync();
    }

    private static async Task<Policy> SeedPublishedPolicyAsync(
        TitanMdmDbContext db,
        Guid organizationId)
    {
        var policy =
            new Policy(
                organizationId,
                "Android",
                null,
                PolicyPlatform.Android,
                UserId);

        db.Policies.Add(policy);

        var publication =
            new AndroidPolicyPublication(
                organizationId,
                policy.Id,
                Guid.NewGuid(),
                policy.CurrentVersion,
                policy.Id.ToString());

        publication.BeginPublication("{}");

        publication.MarkPublished(
            $"enterprises/LC000000/policies/{policy.Id}",
            "{}");

        db.AndroidPolicyPublications.Add(publication);

        await db.SaveChangesAsync();

        return policy;
    }

    private static AndroidEnterpriseService CreateService(
        TitanMdmDbContext db,
        HttpMessageHandler? handler = null)
    {
        var options =
            new AndroidManagementOptions
            {
                ProjectId = "titan-test",
                MaxRetryAttempts = 0
            };

        var httpClient =
            new HttpClient(
                handler ?? new StubHandler())
            {
                BaseAddress =
                    new Uri(
                        AndroidManagementOptions
                            .DefaultBaseUrl)
            };

        var client =
            new AndroidManagementClient(
                httpClient,
                Options.Create(options),
                new StubTokenProvider(),
                NullLogger<
                    AndroidManagementClient>.Instance);

        return new AndroidEnterpriseService(
            db,
            client,
            new StubTokenProvider(),
            Options.Create(options));
    }

    private static TitanMdmDbContext Open()
    {
        var options =
            new DbContextOptionsBuilder<
                    TitanMdmDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid()
                        .ToString("N"))
                .Options;

        return new TitanMdmDbContext(
            options);
    }

    private sealed class StubTokenProvider
        : IGoogleAndroidAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                "stub-token");
        }

        public Task<bool> CanAuthenticateAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                true);
        }
    }

    private sealed class StubHandler
        : HttpMessageHandler
    {
        public string? LastRequestBody
        {
            get;
            private set;
        }

        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            LastRequestBody =
                request.Content is null
                    ? null
                    : request.Content
                        .ReadAsStringAsync(
                            cancellationToken)
                        .GetAwaiter()
                        .GetResult();

            const string body =
                """
                {
                  "name": "enterprises/LC000000/enrollmentTokens/token-1",
                  "value": "ENROLL-TOKEN-VALUE",
                  "qrCode": "data:image/png;base64,AAAA"
                }
                """;

            var response =
                new HttpResponseMessage(
                    HttpStatusCode.OK)
                {
                    Content =
                        new StringContent(
                            body,
                            Encoding.UTF8,
                            "application/json")
                };

            return Task.FromResult(response);
        }
    }

    private sealed class FailingHandler
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            const string body =
                """
                {
                  "error": {
                    "code": 400,
                    "message": "Invalid policy name.",
                    "status": "INVALID_ARGUMENT"
                  }
                }
                """;

            var response =
                new HttpResponseMessage(
                    HttpStatusCode.BadRequest)
                {
                    Content =
                        new StringContent(
                            body,
                            Encoding.UTF8,
                            "application/json")
                };

            return Task.FromResult(response);
        }
    }
}
