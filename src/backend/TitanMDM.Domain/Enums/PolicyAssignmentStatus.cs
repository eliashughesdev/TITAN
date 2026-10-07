namespace TitanMDM.Domain.Enums;

public enum PolicyAssignmentStatus
{
    Pending = 1,
    Queued = 2,
    Applying = 3,
    Applied = 4,
    Failed = 5,
    Removed = 6
}