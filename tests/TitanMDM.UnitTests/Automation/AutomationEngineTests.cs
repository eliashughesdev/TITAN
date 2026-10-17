using TitanMDM.Application.Automation;
using TitanMDM.Application.Security;
using TitanMDM.Infrastructure.Automation.Enterprise.Actions;
using TitanMDM.Infrastructure.Automation.Enterprise.Engine;
using TitanMDM.Infrastructure.Automation.Enterprise.Events;
using TitanMDM.Infrastructure.Automation.Enterprise.Integrations;
using TitanMDM.Infrastructure.Automation.Enterprise.Queue;
using TitanMDM.Infrastructure.Automation.Enterprise.Scheduling;
using TitanMDM.Domain.Automation;

namespace TitanMDM.UnitTests.Automation;

public sealed class AutomationEngineTests
{
    [Fact]
    public void ValidDefinitionPasses()
    {
        var definition = Valid();

        var result =
            AutomationDefinitionValidator.Validate(
                definition);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(
            result.Issues,
            x => x.Severity == "error");
    }

    [Fact]
    public void DisconnectedTriggerFails()
    {
        var definition = Valid();
        definition.Trigger = "TicketCreated";

        var result =
            AutomationDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            x => x.Path == "trigger");
    }

    [Fact]
    public void DisabledActionFails()
    {
        var definition = Valid();
        definition.Actions = new List<AutomationActionDto>
        {
            new()
            {
                Code = "AssignTicket",
                Order = 1
            }
        };

        var result =
            AutomationDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ScheduleTriggerRequiresSchedule()
    {
        var definition = Valid();
        definition.Trigger = "Schedule";

        var result =
            AutomationDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            x => x.Path == "schedule");
    }

    [Fact]
    public void OnceScheduleRequiresFutureStart()
    {
        var definition = Valid();
        definition.Trigger = "Schedule";
        definition.Schedule =
            new AutomationScheduleDefinitionDto
            {
                Recurrence = "Once",
                TimeZoneId = "UTC",
                StartAtUtc = null
            };

        var result =
            AutomationDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            x => x.Path == "schedule.startAtUtc");

        definition.Schedule.StartAtUtc =
            DateTime.UtcNow.AddHours(1);

        result = AutomationDefinitionValidator.Validate(
            definition);

        Assert.DoesNotContain(
            result.Issues,
            x => x.Path == "schedule.startAtUtc");
    }

    [Fact]
    public void SerializerRoundTripsDefinition()
    {
        var definition = Valid();
        definition.Priority = 321;
        definition.ScopeSiteIds.Add(
            Guid.NewGuid());

        var json =
            AutomationDefinitionSerializer
                .Serialize(definition);

        var clone =
            AutomationDefinitionSerializer
                .Deserialize<AutomationRuleDefinition>(
                    json);

        Assert.NotNull(clone);
        Assert.Equal(321, clone!.Priority);
        Assert.Equal(
            definition.Actions.Count,
            clone.Actions.Count);
        Assert.Single(clone.ScopeSiteIds);
    }

    [Fact]
    public void ConditionGroupAllRequiresEveryCondition()
    {
        var group = new AutomationConditionGroupDto
        {
            Operator = "All",
            Conditions =
            {
                new()
                {
                    Field = "status",
                    Operator = "Eq",
                    Value = "offline"
                },
                new()
                {
                    Field = "batteryLevel",
                    Operator = "LessThan",
                    Value = "20"
                }
            }
        };

        var payload =
            "{\"status\":\"offline\",\"batteryLevel\":10}";

        var evaluation =
            AutomationConditionEvaluator.Evaluate(
                group,
                payload);

        Assert.True(evaluation.Passed);
    }

    [Fact]
    public void ConditionGroupAnyShortCircuits()
    {
        var group = new AutomationConditionGroupDto
        {
            Operator = "Any",
            Conditions =
            {
                new()
                {
                    Field = "status",
                    Operator = "Eq",
                    Value = "online"
                },
                new()
                {
                    Field = "status",
                    Operator = "Contains",
                    Value = "line"
                }
            }
        };

        var evaluation =
            AutomationConditionEvaluator.Evaluate(
                group,
                "{\"status\":\"offline\"}");

        Assert.True(evaluation.Passed);
    }

    [Fact]
    public void ConditionInOperatorMatchesList()
    {
        var group = new AutomationConditionGroupDto
        {
            Operator = "Any",
            Conditions =
            {
                new()
                {
                    Field = "status",
                    Operator = "In",
                    Values = new List<string>
                    {
                        "lost",
                        "offline"
                    }
                }
            }
        };

        var evaluation =
            AutomationConditionEvaluator.Evaluate(
                group,
                "{\"status\":\"offline\"}");

        Assert.True(evaluation.Passed);
    }

    [Fact]
    public void MissingFieldFailsEquality()
    {
        var group = new AutomationConditionGroupDto
        {
            Operator = "All",
            Conditions =
            {
                new()
                {
                    Field = "status",
                    Operator = "Eq",
                    Value = "offline"
                }
            }
        };

        var evaluation =
            AutomationConditionEvaluator.Evaluate(
                group,
                "{}");

        Assert.False(evaluation.Passed);
    }

    [Fact]
    public void ScheduleCalculatorDailyRollsToNextDay()
    {
        var from = new DateTime(
            2026,
            1,
            1,
            9,
            0,
            0,
            DateTimeKind.Utc);

        var next =
            AutomationScheduleCalculator.ComputeNext(
                AutomationScheduleRecurrence.Daily,
                from,
                null,
                "08:00",
                null,
                null,
                "UTC",
                null);

        Assert.Equal(
            new DateTime(
                2026,
                1,
                2,
                8,
                0,
                0,
                DateTimeKind.Utc),
            next);
    }

    [Fact]
    public void ScheduleCalculatorIntervalUsesAnchor()
    {
        var from = new DateTime(
            2026,
            1,
            1,
            12,
            0,
            0,
            DateTimeKind.Utc);

        var anchor = from.AddMinutes(-5);

        var next =
            AutomationScheduleCalculator.ComputeNext(
                AutomationScheduleRecurrence.Interval,
                from,
                30,
                null,
                null,
                null,
                "UTC",
                anchor);

        Assert.Equal(
            anchor.AddMinutes(30),
            next);
    }

    [Fact]
    public void ScheduleCalculatorOnceReturnsFutureAnchor()
    {
        var from = new DateTime(
            2026,
            1,
            1,
            12,
            0,
            0,
            DateTimeKind.Utc);

        var anchor = from.AddHours(1);

        var next =
            AutomationScheduleCalculator.ComputeNext(
                AutomationScheduleRecurrence.Once,
                from,
                null,
                null,
                null,
                null,
                "UTC",
                anchor);

        Assert.Equal(anchor, next);
    }

    [Fact]
    public void ActionCatalogListsEnabledHighImpactActions()
    {
        var disabled =
            AutomationActionCatalog.All
                .Where(x => !x.Enabled)
                .ToArray();

        Assert.NotEmpty(disabled);
        Assert.All(
            disabled,
            x => Assert.Equal("high", x.Impact));

        Assert.NotNull(
            AutomationActionCatalog.Find(
                "CreateNotification"));

        Assert.True(
            AutomationEventCatalog.IsConnected(
                "Manual"));

        Assert.False(
            AutomationEventCatalog.IsConnected(
                "TicketCreated"));
    }

    [Fact]
    public void UnknownActionParameterFails()
    {
        var definition = Valid();

        definition.Actions[0].Parameters["bogus"] = "x";

        var result =
            AutomationDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Issues,
            x => x.Path.Contains("bogus"));
    }

    [Fact]
    public void RequiredActionOverlongParameterFails()
    {
        var definition = Valid();

        definition.Actions[0].Parameters["message"] =
            new string('x', 5000);

        var result =
            AutomationDefinitionValidator.Validate(
                definition);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CatalogMapsRequiredPermissionsPerAction()
    {
        Assert.Equal(
            PermissionCodes.Devices.Commands,
            AutomationActionCatalog.RequiredPermissionFor(
                "RestartDevice"));

        Assert.Equal(
            PermissionCodes.Helpdesk.TicketsAssign,
            AutomationActionCatalog.RequiredPermissionFor(
                "EscalateTicket"));

        Assert.Null(
            AutomationActionCatalog.RequiredPermissionFor(
                "CreateNotification"));

        Assert.True(
            AutomationActionCatalog.AllowsUnknownParameters(
                "SendCommand"));

        Assert.False(
            AutomationActionCatalog.AllowsUnknownParameters(
                "CreateNotification"));
    }

    [Fact]
    public void AutomationPermissionsExposeGranularCodes()
    {
        var codes = AutomationPermissions.All
            .Select(x => x.Code)
            .ToArray();

        Assert.Contains(
            AutomationPermissions.Create,
            codes);

        Assert.Contains(
            AutomationPermissions.Edit,
            codes);

        Assert.Contains(
            AutomationPermissions.Admin,
            codes);

        Assert.Contains(
            AutomationPermissions.Manage,
            AutomationPermissions.GrantingCodes(
                AutomationPermissions.Create));

        Assert.Contains(
            AutomationPermissions.Admin,
            AutomationPermissions.GrantingCodes(
                AutomationPermissions.Execute));

        Assert.DoesNotContain(
            PermissionCodes.Devices.Commands,
            AutomationPermissions.GrantingCodes(
                AutomationPermissions.Admin));
    }

    [Fact]
    public void IntegrationCatalogSeparatesConnectedAndPreparedModules()
    {
        var general = Assert.Single(
            AutomationIntegrationCatalog.All,
            x => x.Module == AutomationModules.General);

        var android = Assert.Single(
            AutomationIntegrationCatalog.All,
            x => x.Module == AutomationModules.Android);

        Assert.True(general.Connected);
        Assert.Equal("connected", general.Status);
        Assert.Contains("DeviceOnline", general.EventCodes);

        Assert.False(android.Connected);
        Assert.Equal("prepared", android.Status);
        Assert.Contains("AndroidEnrolled", android.EventCodes);
    }

    [Fact]
    public void Aut2ToolContractsKeepWritesDisabled()
    {
        Assert.NotEmpty(AutomationToolContractCatalog.All);

        Assert.All(
            AutomationToolContractCatalog.All,
            contract => Assert.False(
                contract.WriteEnabled));

        var assistant = Assert.Single(
            AutomationToolContractCatalog.All,
            x => x.ToolCode == "RunAssistantTool");

        Assert.True(assistant.RequiresApproval);
        Assert.Equal(
            AutomationPermissions.Admin,
            assistant.RequiredPermission);
    }

    [Fact]
    public async Task ScheduleTaskPreservesActorAndIncrementsCycleDepth()
    {
        var queue = new CapturingQueueService();
        var handler = new ScheduleTaskActionHandler(queue);
        var actorId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();

        var result = await handler.ExecuteAsync(
            new AutomationActionContext(
                Guid.NewGuid(),
                null,
                actorId,
                ruleId,
                null,
                new AutomationActionDto
                {
                    Code = "ScheduleTask",
                    Parameters = new Dictionary<string, string?>
                    {
                        ["delaySeconds"] = "5",
                        ["actionCode"] = "CreateNotification",
                        ["actionParamsJson"] = "{}"
                    }
                },
                "{}",
                false,
                1,
                Array.Empty<Guid>()));

        Assert.True(result.Success);
        Assert.NotNull(queue.Item);
        Assert.Equal(2, queue.Item!.CycleDepth);
        Assert.Equal(actorId, queue.Item.ActorUserId);
        Assert.Equal(ruleId, queue.Item.AutomationRuleId);
        Assert.Equal("ScheduledTask", queue.Item.TriggerType);
    }

    [Fact]
    public async Task ScheduleTaskBlocksExcessiveCycleDepth()
    {
        var queue = new CapturingQueueService();
        var handler = new ScheduleTaskActionHandler(queue);

        var result = await handler.ExecuteAsync(
            new AutomationActionContext(
                Guid.NewGuid(),
                null,
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                new AutomationActionDto
                {
                    Code = "ScheduleTask",
                    Parameters = new Dictionary<string, string?>
                    {
                        ["delaySeconds"] = "5",
                        ["actionCode"] = "ScheduleTask",
                        ["actionParamsJson"] = "{}"
                    }
                },
                "{}",
                false,
                AutomationPolicyDefaults.MaxCycleDepth,
                Array.Empty<Guid>()));

        Assert.False(result.Success);
        Assert.Null(queue.Item);
    }

    private static AutomationRuleDefinition Valid()
    {
        return new AutomationRuleDefinition
        {
            Module = AutomationModules.General,
            Trigger = "Manual",
            Priority = 100,
            Actions = new List<AutomationActionDto>
            {
                new()
                {
                    Code = "CreateNotification",
                    Order = 1,
                    Parameters = new Dictionary<
                        string,
                        string?>
                    {
                        ["title"] = "Hello",
                        ["message"] = "World"
                    }
                }
            }
        };
    }

    private sealed class CapturingQueueService
        : IAutomationQueueService
    {
        public AutomationQueueItem? Item { get; private set; }

        public Task<AutomationQueueItem> EnqueueAsync(
            AutomationQueueItem item,
            CancellationToken cancellationToken = default)
        {
            Item = item;
            return Task.FromResult(item);
        }

        public Task<IReadOnlyList<AutomationQueueItem>>
            ClaimDueAsync(
                int max,
                TimeSpan lease,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult<
                IReadOnlyList<AutomationQueueItem>>(
                Array.Empty<AutomationQueueItem>());
        }

        public Task<int> RecoverExpiredLeasesAsync(
            TimeSpan lease,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task<int> CountActiveForOrganizationAsync(
            Guid organizationId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }
}
