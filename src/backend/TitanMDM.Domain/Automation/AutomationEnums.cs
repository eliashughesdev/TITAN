namespace TitanMDM.Domain.Automation;

public enum AutomationTriggerType
{
    Manual = 0,

    DeviceEnrolled = 1,

    DeviceOnline = 2,

    DeviceOffline = 3,

    DeviceNonCompliant = 4,

    SecurityRisk = 5,

    GeofenceEnter = 6,

    GeofenceExit = 7,

    LowBattery = 8,

    LostModeActivated = 9,

    Schedule = 10
}

public enum AutomationActionType
{
    SendCommand = 0,

    RequestLocation = 1,

    SecurityScan = 2,

    ComplianceScan = 3,

    AppInventory = 4,

    EnableLostMode = 5,

    DisableLostMode = 6,

    Notification = 7,

    WindowsInventory = 8,

    WindowsUpdateScan = 9,

    WindowsUpdateInstall = 10,

    RestartDevice = 11,

    LockDevice = 12
}

public enum AutomationExecutionStatus
{
    Running = 0,

    Success = 1,

    Failed = 2,

    Skipped = 3
}