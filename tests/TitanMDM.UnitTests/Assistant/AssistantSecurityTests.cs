using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using TitanMDM.Application.Assistant;
using TitanMDM.Application.Audit;
using TitanMDM.Application.Security;
using TitanMDM.Infrastructure.Assistant;
using TitanMDM.Infrastructure.Assistant.Tools;

namespace TitanMDM.UnitTests.Assistant;

public sealed class AssistantSecurityTests
{
    private static AssistantPrincipal Principal(
        params string[] permissions)
    {
        return new AssistantPrincipal(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Cesar Iglesias",
            "cesar@example.com",
            true,
            new[] { "Operator" },
            permissions);
    }

    private static AssistantToolContext Context(
        AssistantPrincipal principal,
        IReadOnlyCollection<Guid>? sites = null)
    {
        return new AssistantToolContext(
            principal,
            new AuthorizationScopeSnapshot(
                true,
                Array.Empty<Guid>()),
            AccessibleSiteIds:
                sites ?? Array.Empty<Guid>(),
            "devices",
            "10.0.0.5");
    }

    private static AssistantOrchestrator CreateOrchestrator(
        IAssistantProvider? provider,
        IEnumerable<IAssistantTool> tools,
        AssistantOptions? options = null,
        IAssistantQuota? quota = null,
        IAdministrativeAuditWriter? auditWriter = null,
        IAssistantSessionStore? store = null)
    {
        var effectiveOptions =
            options ??
            new AssistantOptions();

        store ??=
            new InMemoryAssistantSessionStore(
                Options.Create(
                    effectiveOptions),
                NullLogger<
                    InMemoryAssistantSessionStore>.Instance);

        return new AssistantOrchestrator(
            new AssistantToolRegistry(
                tools),
            store,
            new StubProviderRegistry(
                provider),
            quota ??
                new CountingQuota(),
            auditWriter ??
                new RecordingAuditWriter(),
            Options.Create(
                effectiveOptions),
            NullLogger<
                AssistantOrchestrator>.Instance);
    }

    private static StubTool DeviceTool(
        Func<System.Text.Json.Nodes.JsonObject, AssistantToolResult>? handler = null)
    {
        return new StubTool(
            new AssistantToolDescriptor(
                "devices.list",
                "Listar",
                "desc",
                AssistantToolKind.Read,
                Array.Empty<string>()),
            handler);
    }

    [Fact]
    public async Task Turn_PassesIdentityScopeAndSiteToTool()
    {
        var tool =
            DeviceTool(
                _ =>
                    AssistantToolResult.Ok("ok"));

        var provider =
            new ScriptedProvider(
                "{\"toolCalls\":[{\"tool\":\"devices.list\",\"arguments\":{}}],\"answer\":\"\"}",
                "{\"toolCalls\":[],\"answer\":\"listo\"}");

        var orchestrator =
            CreateOrchestrator(
                provider,
                new IAssistantTool[] { tool });

        var principal =
            Principal();

        var siteId =
            Guid.NewGuid();

        var context =
            new AssistantToolContext(
                principal,
                new AuthorizationScopeSnapshot(
                    false,
                    new[] { siteId }),
                new[] { siteId },
                "devices",
                "10.0.0.9");

        await orchestrator.ExecuteTurnAsync(
            principal,
            context,
            new AssistantTurnRequest(
                null,
                "lista dispositivos",
                "devices"));

        var captured =
            tool.CapturedContext;

        Assert.NotNull(
            captured);

        Assert.Equal(
            principal.OrganizationId,
            captured!.Principal.OrganizationId);

        Assert.Equal(
            "10.0.0.9",
            captured.IpAddress);

        Assert.Contains(
            siteId,
            captured.AccessibleSiteIds);
    }

    [Fact]
    public async Task Turn_ProviderThrows_ReturnsFallbackAndAuditsError()
    {
        var audit =
            new RecordingAuditWriter();

        var orchestrator =
            CreateOrchestrator(
                new ThrowingProvider(),
                Array.Empty<IAssistantTool>(),
                auditWriter: audit);

        var principal =
            Principal();

        var result =
            await orchestrator.ExecuteTurnAsync(
                principal,
                new AssistantToolContext(
                    principal,
                    new AuthorizationScopeSnapshot(
                        true,
                        Array.Empty<Guid>()),
                    Array.Empty<Guid>(),
                    "devices"),
                new AssistantTurnRequest(
                    null,
                    "hola asistente",
                    "devices"));

        Assert.False(
            string.IsNullOrWhiteSpace(
                result.Answer));

        Assert.Contains(
            audit.Entries,
            entry =>
                entry.Action == "assistant.turn" &&
                entry.Result == "error");
    }

    [Fact]
    public async Task Turn_AuditFailure_DoesNotBreakTurn()
    {
        var provider =
            new ConstantProvider(
                "{\"toolCalls\":[],\"answer\":\"respuesta\"}");

        var orchestrator =
            CreateOrchestrator(
                provider,
                Array.Empty<IAssistantTool>(),
                auditWriter: new ThrowingAuditWriter());

        var principal =
            Principal();

        var result =
            await orchestrator.ExecuteTurnAsync(
                principal,
                new AssistantToolContext(
                    principal,
                    new AuthorizationScopeSnapshot(
                        true,
                        Array.Empty<Guid>()),
                    Array.Empty<Guid>(),
                    "devices"),
                new AssistantTurnRequest(
                    null,
                    "consulta",
                    "devices"));

        Assert.Equal(
            "respuesta",
            result.Answer);
    }

    [Fact]
    public async Task Turn_OuterCancellation_Propagates()
    {
        var orchestrator =
            CreateOrchestrator(
                new CancellingProvider(),
                Array.Empty<IAssistantTool>(),
                new AssistantOptions
                {
                    RequestTimeoutSeconds =
                        60
                });

        var principal =
            Principal();

        using var cancellation =
            new CancellationTokenSource();

        cancellation.CancelAfter(
            TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                orchestrator.ExecuteTurnAsync(
                    principal,
                    new AssistantToolContext(
                        principal,
                        new AuthorizationScopeSnapshot(
                            true,
                            Array.Empty<Guid>()),
                        Array.Empty<Guid>(),
                        "devices"),
                    new AssistantTurnRequest(
                        null,
                        "consulta lenta",
                        "devices"),
                    cancellation.Token));
    }

    [Fact]
    public async Task Turn_QuotaExceeded_IsRejected()
    {
        var audit =
            new RecordingAuditWriter();

        var provider =
            new ConstantProvider(
                "{\"toolCalls\":[],\"answer\":\"respuesta\"}");

        var orchestrator =
            CreateOrchestrator(
                provider,
                Array.Empty<IAssistantTool>(),
                quota: new FixedQuota(1),
                auditWriter: audit);

        var principal =
            Principal();

        var context =
            new AssistantToolContext(
                principal,
                new AuthorizationScopeSnapshot(
                    true,
                    Array.Empty<Guid>()),
                Array.Empty<Guid>(),
                "devices");

        var first =
            await orchestrator.ExecuteTurnAsync(
                principal,
                context,
                new AssistantTurnRequest(
                    null,
                    "primera consulta",
                    "devices"));

        Assert.Equal(
            "respuesta",
            first.Answer);

        var second =
            await orchestrator.ExecuteTurnAsync(
                principal,
                context,
                new AssistantTurnRequest(
                    null,
                    "segunda consulta",
                    "devices"));

        Assert.Contains(
            "límite",
            second.Answer);

        Assert.Contains(
            audit.Entries,
            entry =>
                entry.Result == "quota_exceeded");
    }

    [Fact]
    public async Task Turn_UnknownTool_IsRejected()
    {
        var provider =
            new ScriptedProvider(
                "{\"toolCalls\":[{\"tool\":\"devices.wipe_all\",\"arguments\":{}}],\"answer\":\"\"}",
                "{\"toolCalls\":[],\"answer\":\"no disponible\"}");

        var orchestrator =
            CreateOrchestrator(
                provider,
                Array.Empty<IAssistantTool>());

        var principal =
            Principal();

        var result =
            await orchestrator.ExecuteTurnAsync(
                principal,
                new AssistantToolContext(
                    principal,
                    new AuthorizationScopeSnapshot(
                        true,
                        Array.Empty<Guid>()),
                    Array.Empty<Guid>(),
                    "devices"),
                new AssistantTurnRequest(
                    null,
                    "borra todo",
                    "devices"));

        var invocation =
            Assert.Single(
                result.ToolInvocations);

        Assert.False(
            invocation.Success);

        Assert.Equal(
            "tool_not_found",
            invocation.ErrorCode);
    }

    [Fact]
    public async Task Turn_ToolOutput_IsSanitizedAndTruncated()
    {
        var noisy =
            "dato\u0007con\u0001control" +
            new string('x', 20000);

        var tool =
            DeviceTool(
                _ =>
                    AssistantToolResult.Ok(
                        "ok",
                        noisy));

        var provider =
            new ScriptedProvider(
                "{\"toolCalls\":[{\"tool\":\"devices.list\",\"arguments\":{}}],\"answer\":\"\"}",
                "{\"toolCalls\":[],\"answer\":\"listo\"}");

        var options =
            new AssistantOptions
            {
                MaxToolOutputCharacters =
                    2000
            };

        var orchestrator =
            CreateOrchestrator(
                provider,
                new IAssistantTool[] { tool },
                options);

        var principal =
            Principal();

        await orchestrator.ExecuteTurnAsync(
            principal,
            new AssistantToolContext(
                principal,
                new AuthorizationScopeSnapshot(
                    true,
                    Array.Empty<Guid>()),
                Array.Empty<Guid>(),
                "devices"),
            new AssistantTurnRequest(
                null,
                "consulta",
                "devices"));

        var toolMessages =
            provider.Requests
                .SelectMany(
                    request =>
                        request.Messages)
                .Where(
                    message =>
                        message.Content.Contains(
                            "BEGIN_UNTRUSTED_TOOL_DATA",
                            StringComparison.Ordinal))
                .ToList();

        Assert.NotEmpty(
            toolMessages);

        var payload =
            toolMessages[0];

        Assert.DoesNotContain(
            '\u0007',
            payload.Content);

        Assert.DoesNotContain(
            '\u0001',
            payload.Content);

        Assert.True(
            payload.Content.Length <
                2100);
    }

    [Fact]
    public async Task Turns_ConcurrentForSameUser_RemainIsolated()
    {
        var store =
            new InMemoryAssistantSessionStore(
                Options.Create(
                    new AssistantOptions()),
                NullLogger<
                    InMemoryAssistantSessionStore>.Instance);

        var orchestrator =
            CreateOrchestrator(
                new ConstantProvider(
                    "{\"toolCalls\":[],\"answer\":\"ok\"}"),
                Array.Empty<IAssistantTool>(),
                store: store);

        var principal =
            Principal();

        var conversationId =
            Guid.NewGuid();

        var context =
            new AssistantToolContext(
                principal,
                new AuthorizationScopeSnapshot(
                    true,
                    Array.Empty<Guid>()),
                Array.Empty<Guid>(),
                "devices");

        var tasks =
            Enumerable
                .Range(0, 16)
                .Select(
                    index =>
                        orchestrator.ExecuteTurnAsync(
                            principal,
                            context,
                            new AssistantTurnRequest(
                                conversationId,
                                $"consulta {index}",
                                "devices")))
                .ToArray();

        var results =
            await Task.WhenAll(tasks);

        Assert.All(
            results,
            result =>
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        result.Answer)));

        Assert.Equal(
            1,
            store.CountActive(
                principal.OrganizationId,
                principal.UserId));
    }
}
