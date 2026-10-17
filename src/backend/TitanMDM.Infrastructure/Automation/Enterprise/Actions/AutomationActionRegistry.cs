using TitanMDM.Application.Audit;
using TitanMDM.Application.Commands;
using TitanMDM.Application.Helpdesk;
using TitanMDM.Infrastructure.Automation.Enterprise.Queue;
using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.Infrastructure.Automation.Enterprise.Actions;

internal interface IAutomationActionRegistry
{
    IAutomationActionHandler? Resolve(string code);
}

internal sealed class AutomationActionRegistry
    : IAutomationActionRegistry
{
    private readonly Dictionary<
        string,
        IAutomationActionHandler> _handlers;

    public AutomationActionRegistry(
        TitanMdmDbContext db,
        IAdministrativeAuditWriter audit,
        IHelpdeskService helpdesk,
        IDeviceCommandService commands,
        IAutomationQueueService queue)
    {
        var handlers =
            new List<IAutomationActionHandler>
            {
                new NotificationActionHandler(db),
                new SendAvisoActionHandler(db),
                new RegisterAlertActionHandler(
                    db,
                    audit),
                new EscalateTicketActionHandler(
                    helpdesk,
                    audit),
                new CreateFollowUpRecordActionHandler(
                    audit),
                new RequestRuleReevaluationActionHandler(
                    db,
                    queue),
                new ScheduleTaskActionHandler(queue),
                new DeviceCommandActionHandler(
                    commands,
                    "RequestLocation",
                    "LOCATION_REQUEST"),
                new DeviceCommandActionHandler(
                    commands,
                    "SecurityScan",
                    "SECURITY_STATUS"),
                new DeviceCommandActionHandler(
                    commands,
                    "ComplianceScan",
                    "COMPLIANCE_CHECK"),
                new DeviceCommandActionHandler(
                    commands,
                    "AppInventory",
                    "APP_INVENTORY"),
                new DeviceCommandActionHandler(
                    commands,
                    "WindowsInventory",
                    "DEVICE_INVENTORY"),
                new DeviceCommandActionHandler(
                    commands,
                    "WindowsUpdateScan",
                    "WINDOWS_UPDATE_SCAN"),
                new DeviceCommandActionHandler(
                    commands,
                    "WindowsUpdateInstall",
                    "WINDOWS_UPDATE_INSTALL"),
                new DeviceCommandActionHandler(
                    commands,
                    "RestartDevice",
                    "RESTART_DEVICE"),
                new DeviceCommandActionHandler(
                    commands,
                    "LockDevice",
                    "LOCK_DEVICE"),
                new DeviceCommandActionHandler(
                    commands,
                    "EnableLostMode",
                    "LOST_MODE_ENABLE"),
                new DeviceCommandActionHandler(
                    commands,
                    "DisableLostMode",
                    "LOST_MODE_DISABLE"),
                new DeviceCommandActionHandler(
                    commands,
                    "SendCommand",
                    "SEND_COMMAND")
            };

        /*
         * Alias de compatibilidad: la acción legacy
         * "Notification" ahora crea una notificación real.
         */
        handlers.Add(
            new AliasActionHandler(
                "Notification",
                handlers.First(x =>
                    x.Code == "CreateNotification")));

        /*
         * Stubs de alto impacto registrados pero
         * deshabilitados (servidos si se invocan a mano).
         */
        handlers.Add(
            new DisabledActionHandler(
                "AssignTicket"));
        handlers.Add(
            new DisabledActionHandler(
                "RequestDeviceSync"));
        handlers.Add(
            new DisabledActionHandler(
                "ApplyPolicy"));
        handlers.Add(
            new DisabledActionHandler(
                "GenerateReport"));
        handlers.Add(
            new DisabledActionHandler(
                "RunAssistantTool"));

        _handlers = handlers
            .GroupBy(
                x => x.Code,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.First(),
                StringComparer.OrdinalIgnoreCase);
    }

    public IAutomationActionHandler? Resolve(
        string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        return _handlers.GetValueOrDefault(
            code.Trim());
    }

    private sealed class AliasActionHandler
        : IAutomationActionHandler
    {
        private readonly IAutomationActionHandler _inner;

        public AliasActionHandler(
            string code,
            IAutomationActionHandler inner)
        {
            Code = code;
            _inner = inner;
        }

        public string Code { get; }

        public Task<AutomationActionResult>
            ExecuteAsync(
                AutomationActionContext context,
                CancellationToken cancellationToken = default)
        {
            return _inner.ExecuteAsync(
                context,
                cancellationToken);
        }
    }
}
