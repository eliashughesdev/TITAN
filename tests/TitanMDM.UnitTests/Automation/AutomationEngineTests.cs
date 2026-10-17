using TitanMDM.Application.Automation;
using TitanMDM.Infrastructure.Automation.Enterprise.Actions;
using TitanMDM.Infrastructure.Automation.Enterprise.Engine;
using TitanMDM.Infrastructure.Automation.Enterprise.Events;
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
}
