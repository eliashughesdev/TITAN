namespace TitanMDM.Domain.Enums;

public enum RemoteSessionStatus
{
    Requested = 0,
    Connecting = 1,
    Connected = 2,
    Disconnecting = 3,
    Completed = 4,
    Failed = 5,
    Expired = 6,
    Cancelled = 7
}