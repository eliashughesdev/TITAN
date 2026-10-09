using Microsoft.EntityFrameworkCore;

namespace TitanMDM.Infrastructure.Helpdesk;

public sealed partial class HelpdeskService
{
    // SQL Server application lock serializes automatic ranking + capacity updates
    // per organization across processes. Released by commit/rollback, never held
    // while calling Graph, AI or SMTP. Other organizations remain independent.
    private Task AcquireAutomaticAssignmentLockAsync(Guid organizationId, CancellationToken ct) =>
        _db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = {$"TitanMDM:Helpdesk:Assignment:{organizationId:D}"},
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @result < 0 THROW 51001, 'Helpdesk assignment lock unavailable.', 1;
            """, ct);
}
