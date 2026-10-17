using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using TitanMDM.Application.Assistant;
using TitanMDM.Infrastructure.Assistant;
using TitanMDM.Infrastructure.Assistant.Tools;

namespace TitanMDM.UnitTests.Assistant;

public sealed class AssistantToolRegistryTests
{
    private static AssistantPrincipal Principal(
        params string[] permissions)
    {
        return new AssistantPrincipal(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Tester",
            "tester@example.com",
            true,
            new[] { "Operator" },
            permissions);
    }

    private static AssistantToolContext Context(
        AssistantPrincipal principal)
    {
        return new AssistantToolContext(
            principal,
            new TitanMDM.Application.Security.AuthorizationScopeSnapshot(
                true,
                Array.Empty<Guid>()),
            Array.Empty<Guid>(),
            "test");
    }

    [Fact]
    public void Describe_OnlyReturnsAuthorizedReadTools()
    {
        var requiresView =
            new StubTool(
                new AssistantToolDescriptor(
                    "devices.list",
                    "Listar",
                    "desc",
                    AssistantToolKind.Read,
                    new[] { "devices.view" }));

        var noPermission =
            new StubTool(
                new AssistantToolDescriptor(
                    "helpdesk.my_tickets",
                    "Tickets",
                    "desc",
                    AssistantToolKind.Read,
                    Array.Empty<string>()));

        var registry =
            new AssistantToolRegistry(
                new IAssistantTool[]
                {
                    requiresView,
                    noPermission
                });

        var withPermission =
            registry.Describe(
                Principal("devices.view"));

        Assert.Equal(
            2,
            withPermission.Count);

        var withoutPermission =
            registry.Describe(
                Principal());

        Assert.Single(
            withoutPermission);

        Assert.Equal(
            "helpdesk.my_tickets",
            withoutPermission[0].Name);
    }

    [Fact]
    public async Task Execute_UnknownTool_ReturnsFailure()
    {
        var registry =
            new AssistantToolRegistry(
                Array.Empty<IAssistantTool>());

        var principal =
            Principal();

        var result =
            await registry.ExecuteAsync(
                "does.not.exist",
                Context(principal),
                new System.Text.Json.Nodes.JsonObject());

        Assert.False(
            result.Success);

        Assert.Equal(
            "tool_not_found",
            result.ErrorCode);
    }

    [Fact]
    public async Task Execute_UnauthorizedTool_DoesNotRun()
    {
        var tool =
            new StubTool(
                new AssistantToolDescriptor(
                    "devices.list",
                    "Listar",
                    "desc",
                    AssistantToolKind.Read,
                    new[] { "devices.view" }));

        var registry =
            new AssistantToolRegistry(
                new IAssistantTool[] { tool });

        var principal =
            Principal();

        var result =
            await registry.ExecuteAsync(
                "devices.list",
                Context(principal),
                new System.Text.Json.Nodes.JsonObject());

        Assert.False(
            result.Success);

        Assert.Equal(
            "forbidden",
            result.ErrorCode);

        Assert.Equal(
            0,
            tool.ExecuteCount);
    }

    [Fact]
    public async Task Execute_NonReadTool_IsNeverRegistered()
    {
        var sensitive =
            new StubTool(
                new AssistantToolDescriptor(
                    "devices.wipe",
                    "Borrar",
                    "desc",
                    AssistantToolKind.Sensitive,
                    Array.Empty<string>()));

        var registry =
            new AssistantToolRegistry(
                new IAssistantTool[] { sensitive });

        var result =
            await registry.ExecuteAsync(
                "devices.wipe",
                Context(Principal()),
                new System.Text.Json.Nodes.JsonObject());

        Assert.False(
            result.Success);

        Assert.Equal(
            "tool_not_found",
            result.ErrorCode);
    }
}

public sealed class InMemoryAssistantSessionStoreTests
{
    private static InMemoryAssistantSessionStore CreateStore(
        int maxSessions = 20)
    {
        return new InMemoryAssistantSessionStore(
            Options.Create(
                new AssistantOptions
                {
                    MaxActiveSessionsPerUser =
                        maxSessions,
                    SessionTtlMinutes =
                        240
                }),
            NullLogger<
                InMemoryAssistantSessionStore>.Instance);
    }

    [Fact]
    public void Sessions_AreIsolatedByOrganizationAndUser()
    {
        var store =
            CreateStore();

        var organizationA =
            Guid.NewGuid();

        var organizationB =
            Guid.NewGuid();

        var userOne =
            Guid.NewGuid();

        var userTwo =
            Guid.NewGuid();

        var conversationId =
            Guid.NewGuid();

        store.Create(
            organizationA,
            userOne,
            conversationId,
            "Hola");

        Assert.Null(
            store.Get(
                organizationA,
                userTwo,
                conversationId));

        Assert.Null(
            store.Get(
                organizationB,
                userOne,
                conversationId));

        Assert.NotNull(
            store.Get(
                organizationA,
                userOne,
                conversationId));
    }

    [Fact]
    public void Append_RejectsForeignConversation()
    {
        var store =
            CreateStore();

        var conversationId =
            Guid.NewGuid();

        store.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            conversationId,
            "Hola");

        Assert.Throws<InvalidOperationException>(
            () =>
                store.Append(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    conversationId,
                    new AssistantConversationEntry(
                        Guid.NewGuid(),
                        "user",
                        "texto",
                        DateTime.UtcNow,
                        Array.Empty<AssistantToolInvocation>())));
    }

    [Fact]
    public void Create_EnforcesPerUserQuota()
    {
        var store =
            CreateStore(
                maxSessions: 2);

        var organizationId =
            Guid.NewGuid();

        var userId =
            Guid.NewGuid();

        store.Create(
            organizationId,
            userId,
            Guid.NewGuid(),
            "Uno");

        Thread.Sleep(
            5);

        store.Create(
            organizationId,
            userId,
            Guid.NewGuid(),
            "Dos");

        Thread.Sleep(
            5);

        store.Create(
            organizationId,
            userId,
            Guid.NewGuid(),
            "Tres");

        Assert.Equal(
            2,
            store.CountActive(
                organizationId,
                userId));
    }

    [Fact]
    public void Append_SetsTitleFromFirstUserMessage()
    {
        var store =
            CreateStore();

        var organizationId =
            Guid.NewGuid();

        var userId =
            Guid.NewGuid();

        var conversationId =
            Guid.NewGuid();

        store.Create(
            organizationId,
            userId,
            conversationId,
            "Nueva conversación");

        store.Append(
            organizationId,
            userId,
            conversationId,
            new AssistantConversationEntry(
                Guid.NewGuid(),
                "user",
                "Necesito ver los dispositivos caídos",
                DateTime.UtcNow,
                Array.Empty<AssistantToolInvocation>()));

        var conversation =
            store.Get(
                organizationId,
                userId,
                conversationId);

        Assert.NotNull(
            conversation);

        Assert.StartsWith(
            "Necesito ver los dispositivos",
            conversation!.Title);
    }
}
