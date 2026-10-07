using TitanMDM.Domain.Entities;

namespace TitanMDM.UnitTests;

public sealed class UserSecurityTests
{
    [Fact]
    public void FailedLogins_EventuallyLockUser()
    {
        var user =
            CreateUser();

        for (
            var attempt = 0;
            attempt < 5;
            attempt++)
        {
            user.RegisterFailedLogin(
                maximumAttempts: 5,
                lockoutMinutes: 15);
        }

        Assert.True(
            user.IsLockedOut);

        Assert.NotNull(
            user.LockedUntilUtc);
    }

    [Fact]
    public void SuccessfulLogin_ClearsLockoutState()
    {
        var user =
            CreateUser();

        for (
            var attempt = 0;
            attempt < 5;
            attempt++)
        {
            user.RegisterFailedLogin(
                5,
                15);
        }

        user.RegisterLogin();

        Assert.False(
            user.IsLockedOut);

        Assert.Equal(
            0,
            user.FailedLoginAttempts);

        Assert.Null(
            user.LockedUntilUtc);
    }

    [Fact]
    public void SecuritySensitiveChange_IncrementsVersion()
    {
        var user =
            CreateUser();

        var version =
            user.SecurityVersion;

        user.Deactivate();

        Assert.True(
            user.SecurityVersion >
            version);
    }

    [Fact]
    public void PasswordChange_IncrementsSecurityVersion()
    {
        var user =
            CreateUser();

        var version =
            user.SecurityVersion;

        user.SetPasswordHash(
            "VALID-HASH-FOR-UNIT-TEST");

        Assert.True(
            user.SecurityVersion >
            version);
    }

    private static User CreateUser()
    {
        return new User(
            Guid.NewGuid(),
            "Titan",
            "Tester",
            $"test-{Guid.NewGuid():N}@titanmdm.local");
    }
}