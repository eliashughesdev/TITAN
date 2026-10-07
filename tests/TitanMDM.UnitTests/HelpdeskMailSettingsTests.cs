using TitanMDM.Domain.Entities;

namespace TitanMDM.UnitTests;

public sealed class HelpdeskMailSettingsTests
{
    // ============================================================
    // CONSTRUCTOR
    // ============================================================

    [Fact]
    public void Constructor_ShouldCreateSafeDefaults()
    {
        var organizationId =
            Guid.NewGuid();

        var settings =
            new HelpdeskMailSettings(
                organizationId);

        Assert.Equal(
            organizationId,
            settings.OrganizationId);

        Assert.False(
            settings.InboundEnabled);

        Assert.False(
            settings.OutboundEnabled);

        Assert.True(
            settings.IgnoreAutomaticMessages);

        Assert.True(
            settings.IgnoreBulkMessages);

        Assert.True(
            settings.IgnoreBounceMessages);

        Assert.True(
            settings.IgnoreNoReplyMessages);

        Assert.Equal(
            60,
            settings.InboundPollSeconds);

        Assert.Equal(
            20,
            settings.OutboundPollSeconds);

        Assert.Equal(
            25,
            settings.BatchSize);

        Assert.Equal(
            8,
            settings.MaxAttempts);

        Assert.Equal(
            0,
            settings.Revision);

        Assert.Null(
            settings.Mailbox);

        Assert.Null(
            settings.AcceptedRecipients);

        Assert.Null(
            settings.ActorUserId);

        Assert.Null(
            settings.BlockedSenders);

        Assert.Null(
            settings.BlockedDomains);

        Assert.Null(
            settings.AllowedSenders);

        Assert.Null(
            settings.AllowedDomains);

        Assert.Null(
            settings.IgnoredSubjectPatterns);
    }

    // ============================================================
    // NORMALIZATION
    // ============================================================

    [Fact]
    public void Configure_ShouldNormalizeMailbox()
    {
        var settings =
            CreateSettings();

        settings.Configure(
            mailbox:
                " HELPDESK@EXAMPLE.COM ",

            acceptedRecipients:
                " HELPDESK@EXAMPLE.COM ",

            actorUserId:
                Guid.NewGuid(),

            inboundEnabled:
                true,

            outboundEnabled:
                true,

            ignoreAutomaticMessages:
                true,

            ignoreBulkMessages:
                true,

            ignoreBounceMessages:
                true,

            ignoreNoReplyMessages:
                true,

            blockedSenders:
                null,

            blockedDomains:
                null,

            allowedSenders:
                null,

            allowedDomains:
                null,

            ignoredSubjectPatterns:
                null,

            inboundPollSeconds:
                60,

            outboundPollSeconds:
                20,

            batchSize:
                25,

            maxAttempts:
                8);

        Assert.Equal(
            "helpdesk@example.com",
            settings.Mailbox);

        Assert.Equal(
            "helpdesk@example.com",
            settings.AcceptedRecipients);
    }

    [Fact]
    public void Configure_ShouldDefaultAcceptedRecipientsToMailbox()
    {
        var settings =
            CreateSettings();

        settings.Configure(
            mailbox:
                "helpdesk@example.com",

            acceptedRecipients:
                null,

            actorUserId:
                Guid.NewGuid(),

            inboundEnabled:
                true,

            outboundEnabled:
                false,

            ignoreAutomaticMessages:
                true,

            ignoreBulkMessages:
                true,

            ignoreBounceMessages:
                true,

            ignoreNoReplyMessages:
                true,

            blockedSenders:
                null,

            blockedDomains:
                null,

            allowedSenders:
                null,

            allowedDomains:
                null,

            ignoredSubjectPatterns:
                null,

            inboundPollSeconds:
                60,

            outboundPollSeconds:
                20,

            batchSize:
                25,

            maxAttempts:
                8);

        Assert.Equal(
            "helpdesk@example.com",
            settings.AcceptedRecipients);
    }

    [Fact]
    public void Configure_ShouldNormalizeEnterpriseLists()
    {
        var settings =
            CreateSettings();

        settings.Configure(
            mailbox:
                "helpdesk@example.com",

            acceptedRecipients:
                "HELPDESK@EXAMPLE.COM; SUPPORT@EXAMPLE.COM",

            actorUserId:
                Guid.NewGuid(),

            inboundEnabled:
                true,

            outboundEnabled:
                false,

            ignoreAutomaticMessages:
                true,

            ignoreBulkMessages:
                true,

            ignoreBounceMessages:
                true,

            ignoreNoReplyMessages:
                true,

            blockedSenders:
                "SPAMMER@BAD.COM; noreply@bad.com",

            blockedDomains:
                "BAD.COM; ADS.EXAMPLE",

            allowedSenders:
                "VIP@EXAMPLE.COM",

            allowedDomains:
                "CESARIGLESIAS.COM.DO",

            ignoredSubjectPatterns:
                "NEWSLETTER; PROMOCIÓN",

            inboundPollSeconds:
                60,

            outboundPollSeconds:
                20,

            batchSize:
                25,

            maxAttempts:
                8);

        Assert.Equal(
            "helpdesk@example.com|support@example.com",
            settings.AcceptedRecipients);

        Assert.Equal(
            "noreply@bad.com|spammer@bad.com",
            settings.BlockedSenders);

        Assert.Equal(
            "ads.example|bad.com",
            settings.BlockedDomains);

        Assert.Equal(
            "vip@example.com",
            settings.AllowedSenders);

        Assert.Equal(
            "cesariglesias.com.do",
            settings.AllowedDomains);

        Assert.Equal(
            "newsletter|promoción",
            settings.IgnoredSubjectPatterns);
    }

    // ============================================================
    // REVISION
    // ============================================================

    [Fact]
    public void Configure_ShouldIncrementRevision()
    {
        var settings =
            CreateSettings();

        ConfigureValid(
            settings);

        Assert.Equal(
            1,
            settings.Revision);

        ConfigureValid(
            settings,
            inboundPollSeconds:
                90,
            outboundPollSeconds:
                30,
            batchSize:
                50,
            maxAttempts:
                10);

        Assert.Equal(
            2,
            settings.Revision);
    }

    [Fact]
    public void RuntimeStatus_ShouldNotChangeRevision()
    {
        var settings =
            CreateSettings();

        ConfigureValid(
            settings);

        var revision =
            settings.Revision;

        settings.MarkInboundAttempt();

        settings.MarkInboundSuccess();

        settings.MarkOutboundAttempt();

        settings.MarkOutboundSuccess();

        Assert.Equal(
            revision,
            settings.Revision);
    }

    // ============================================================
    // REQUIRED CONFIGURATION
    // ============================================================

    [Fact]
    public void EnabledMail_ShouldRequireMailbox()
    {
        var settings =
            CreateSettings();

        Assert.Throws<
            ArgumentException>(
            () =>
                settings.Configure(
                    mailbox:
                        null,

                    acceptedRecipients:
                        null,

                    actorUserId:
                        Guid.NewGuid(),

                    inboundEnabled:
                        true,

                    outboundEnabled:
                        false,

                    ignoreAutomaticMessages:
                        true,

                    ignoreBulkMessages:
                        true,

                    ignoreBounceMessages:
                        true,

                    ignoreNoReplyMessages:
                        true,

                    blockedSenders:
                        null,

                    blockedDomains:
                        null,

                    allowedSenders:
                        null,

                    allowedDomains:
                        null,

                    ignoredSubjectPatterns:
                        null,

                    inboundPollSeconds:
                        60,

                    outboundPollSeconds:
                        20,

                    batchSize:
                        25,

                    maxAttempts:
                        8));
    }

    [Fact]
    public void InboundMail_ShouldRequireActor()
    {
        var settings =
            CreateSettings();

        Assert.Throws<
            ArgumentException>(
            () =>
                settings.Configure(
                    mailbox:
                        "helpdesk@example.com",

                    acceptedRecipients:
                        "helpdesk@example.com",

                    actorUserId:
                        null,

                    inboundEnabled:
                        true,

                    outboundEnabled:
                        false,

                    ignoreAutomaticMessages:
                        true,

                    ignoreBulkMessages:
                        true,

                    ignoreBounceMessages:
                        true,

                    ignoreNoReplyMessages:
                        true,

                    blockedSenders:
                        null,

                    blockedDomains:
                        null,

                    allowedSenders:
                        null,

                    allowedDomains:
                        null,

                    ignoredSubjectPatterns:
                        null,

                    inboundPollSeconds:
                        60,

                    outboundPollSeconds:
                        20,

                    batchSize:
                        25,

                    maxAttempts:
                        8));
    }

    // ============================================================
    // POLLING VALIDATION
    // ============================================================

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(3601)]
    public void InvalidInboundPoll_ShouldFail(
        int seconds)
    {
        var settings =
            CreateSettings();

        Assert.Throws<
            ArgumentException>(
            () =>
                ConfigureValid(
                    settings,
                    inboundPollSeconds:
                        seconds));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(3601)]
    public void InvalidOutboundPoll_ShouldFail(
        int seconds)
    {
        var settings =
            CreateSettings();

        Assert.Throws<
            ArgumentException>(
            () =>
                ConfigureValid(
                    settings,
                    outboundPollSeconds:
                        seconds));
    }

    // ============================================================
    // BATCH / RETRIES
    // ============================================================

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void InvalidBatchSize_ShouldFail(
        int batchSize)
    {
        var settings =
            CreateSettings();

        Assert.Throws<
            ArgumentException>(
            () =>
                ConfigureValid(
                    settings,
                    batchSize:
                        batchSize));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void InvalidMaxAttempts_ShouldFail(
        int maxAttempts)
    {
        var settings =
            CreateSettings();

        Assert.Throws<
            ArgumentException>(
            () =>
                ConfigureValid(
                    settings,
                    maxAttempts:
                        maxAttempts));
    }

    // ============================================================
    // FILTER FLAGS
    // ============================================================

    [Fact]
    public void Configure_ShouldPersistEnterpriseFilterFlags()
    {
        var settings =
            CreateSettings();

        settings.Configure(
            mailbox:
                "helpdesk@example.com",

            acceptedRecipients:
                "helpdesk@example.com",

            actorUserId:
                Guid.NewGuid(),

            inboundEnabled:
                true,

            outboundEnabled:
                false,

            ignoreAutomaticMessages:
                false,

            ignoreBulkMessages:
                false,

            ignoreBounceMessages:
                false,

            ignoreNoReplyMessages:
                false,

            blockedSenders:
                null,

            blockedDomains:
                null,

            allowedSenders:
                null,

            allowedDomains:
                null,

            ignoredSubjectPatterns:
                null,

            inboundPollSeconds:
                60,

            outboundPollSeconds:
                20,

            batchSize:
                25,

            maxAttempts:
                8);

        Assert.False(
            settings.IgnoreAutomaticMessages);

        Assert.False(
            settings.IgnoreBulkMessages);

        Assert.False(
            settings.IgnoreBounceMessages);

        Assert.False(
            settings.IgnoreNoReplyMessages);
    }

    // ============================================================
    // RUNTIME STATUS
    // ============================================================

    [Fact]
    public void MarkInboundSuccess_ShouldClearError()
    {
        var settings =
            CreateSettings();

        settings.MarkInboundFailure(
            "Graph failed.");

        Assert.NotNull(
            settings.LastInboundError);

        settings.MarkInboundSuccess();

        Assert.Null(
            settings.LastInboundError);

        Assert.NotNull(
            settings.LastInboundSuccessAtUtc);

        Assert.NotNull(
            settings.LastInboundAttemptAtUtc);
    }

    [Fact]
    public void MarkOutboundSuccess_ShouldClearError()
    {
        var settings =
            CreateSettings();

        settings.MarkOutboundFailure(
            "Send failed.");

        Assert.NotNull(
            settings.LastOutboundError);

        settings.MarkOutboundSuccess();

        Assert.Null(
            settings.LastOutboundError);

        Assert.NotNull(
            settings.LastOutboundSuccessAtUtc);

        Assert.NotNull(
            settings.LastOutboundAttemptAtUtc);
    }

    [Fact]
    public void RuntimeError_ShouldBeTruncated()
    {
        var settings =
            CreateSettings();

        var longError =
            new string(
                'x',
                5000);

        settings.MarkInboundFailure(
            longError);

        Assert.NotNull(
            settings.LastInboundError);

        Assert.Equal(
            2000,
            settings
                .LastInboundError!
                .Length);
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private static HelpdeskMailSettings
        CreateSettings()
    {
        return new HelpdeskMailSettings(
            Guid.NewGuid());
    }

    private static void ConfigureValid(
        HelpdeskMailSettings settings,
        int inboundPollSeconds = 60,
        int outboundPollSeconds = 20,
        int batchSize = 25,
        int maxAttempts = 8)
    {
        settings.Configure(
            mailbox:
                "helpdesk@example.com",

            acceptedRecipients:
                "helpdesk@example.com",

            actorUserId:
                Guid.NewGuid(),

            inboundEnabled:
                true,

            outboundEnabled:
                true,

            ignoreAutomaticMessages:
                true,

            ignoreBulkMessages:
                true,

            ignoreBounceMessages:
                true,

            ignoreNoReplyMessages:
                true,

            blockedSenders:
                null,

            blockedDomains:
                null,

            allowedSenders:
                null,

            allowedDomains:
                null,

            ignoredSubjectPatterns:
                null,

            inboundPollSeconds:
                inboundPollSeconds,

            outboundPollSeconds:
                outboundPollSeconds,

            batchSize:
                batchSize,

            maxAttempts:
                maxAttempts);
    }
}