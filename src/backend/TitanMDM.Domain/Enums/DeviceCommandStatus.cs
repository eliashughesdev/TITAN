namespace TitanMDM.Domain.Enums;

public enum DeviceCommandStatus
{
    Pending = 0,
    Queued = 1,
    Dispatching = 2,
    Sent = 3,
    Delivered = 4,
    Executing = 5,
    Success = 6,
    Failed = 7,
    Timeout = 8,
    Cancelled = 9
}