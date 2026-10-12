using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TitanMDM.Api.Controllers;
using TitanMDM.Api.Services;
using TitanMDM.Application.Enrollment;
using TitanMDM.Application.Security;

namespace TitanMDM.IntegrationTests;

public sealed class WindowsAgentDistributionControllerTests
{
    [Fact]
    public async Task GenerationFailureRevokesCreatedToken()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var enrollment = new FakeEnrollmentService();
        var distribution = new FailingDistributionService();
        var controller = CreateController(
            distribution,
            enrollment,
            organizationId,
            userId,
            includeManagePermission: true);

        var result =
            await controller.GenerateInstaller(
                new CreateWindowsInstallerRequest(
                    "individual",
                    60,
                    1));

        var objectResult =
            Assert.IsType<ObjectResult>(result);

        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            objectResult.StatusCode);
        Assert.True(distribution.PrerequisitesValidated);
        Assert.Equal(
            enrollment.CreatedTokenId,
            enrollment.RevokedTokenId);
    }

    [Fact]
    public async Task MissingManagePermissionDoesNotCreateToken()
    {
        var enrollment = new FakeEnrollmentService();
        var controller = CreateController(
            new FailingDistributionService(),
            enrollment,
            Guid.NewGuid(),
            Guid.NewGuid(),
            includeManagePermission: false);

        var result =
            await controller.GenerateInstaller(
                new CreateWindowsInstallerRequest(
                    "individual",
                    60,
                    1));

        Assert.IsType<ForbidResult>(result);
        Assert.False(enrollment.TokenCreated);
    }

    private static WindowsAgentDistributionController
        CreateController(
            IWindowsAgentDistributionService distribution,
            IEnrollmentService enrollment,
            Guid organizationId,
            Guid userId,
            bool includeManagePermission)
    {
        var claims =
            new List<Claim>
            {
                new("organization_id", organizationId.ToString()),
                new(ClaimTypes.NameIdentifier, userId.ToString())
            };

        if (includeManagePermission)
        {
            claims.Add(
                new Claim(
                    "permission",
                    PermissionCodes.Enrollment.Manage));
        }

        var controller =
            new WindowsAgentDistributionController(
                distribution,
                enrollment,
                NullLogger<WindowsAgentDistributionController>.Instance)
            {
                ControllerContext =
                    new ControllerContext
                    {
                        HttpContext =
                            new DefaultHttpContext
                            {
                                User =
                                    new ClaimsPrincipal(
                                        new ClaimsIdentity(
                                            claims,
                                            "test"))
                            }
                    }
            };

        return controller;
    }

    private sealed class FailingDistributionService
        : IWindowsAgentDistributionService
    {
        public bool PrerequisitesValidated
        {
            get;
            private set;
        }

        public string GetPackagePath() =>
            throw new NotSupportedException();

        public string GetPackageFileName() =>
            throw new NotSupportedException();

        public void ValidateInstallerPrerequisites(
            string deploymentMode)
        {
            PrerequisitesValidated = true;
        }

        public Task<string> GetPackageSha256Async(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<WindowsDistributionArtifact>
            BuildIndividualInstallerAsync(
                string enrollmentToken,
                CancellationToken cancellationToken = default) =>
            throw new IOException("Simulated compiler failure.");

        public Task<WindowsDistributionArtifact>
            BuildGpoPackageAsync(
                string enrollmentToken,
                CancellationToken cancellationToken = default) =>
            throw new IOException("Simulated package failure.");
    }

    private sealed class FakeEnrollmentService
        : IEnrollmentService
    {
        public Guid CreatedTokenId
        {
            get;
            private set;
        }

        public Guid? RevokedTokenId
        {
            get;
            private set;
        }

        public bool TokenCreated =>
            CreatedTokenId != Guid.Empty;

        public Task<CreatedEnrollmentTokenDto>
            CreateTokenAsync(
                Guid organizationId,
                Guid createdByUserId,
                CreateEnrollmentTokenRequest request,
                CancellationToken cancellationToken = default)
        {
            CreatedTokenId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            return Task.FromResult(
                new CreatedEnrollmentTokenDto(
                    CreatedTokenId,
                    "secret-not-for-logs",
                    "Windows",
                    "Active",
                    request.MaxUses,
                    0,
                    now.AddMinutes(request.ExpirationMinutes),
                    now));
        }

        public Task<IReadOnlyCollection<EnrollmentTokenDto>>
            GetTokensAsync(
                Guid organizationId,
                CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<EnrollmentTokenDto>>(
                Array.Empty<EnrollmentTokenDto>());

        public Task<bool> RevokeTokenAsync(
            Guid organizationId,
            Guid enrollmentTokenId,
            CancellationToken cancellationToken = default)
        {
            RevokedTokenId = enrollmentTokenId;
            return Task.FromResult(true);
        }

        public Task<EnrollmentTokenValidationResult>
            ValidateTokenAsync(
                string token,
                string platform,
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
