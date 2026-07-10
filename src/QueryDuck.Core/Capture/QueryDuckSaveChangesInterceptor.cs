using Microsoft.EntityFrameworkCore.Diagnostics;
using QueryDuck.Core.Capture;

namespace QueryDuck.Core.Capture;

public sealed class QueryDuckSaveChangesInterceptor : SaveChangesInterceptor
{
    private int _saveChangesCount;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        TrackSaveChanges(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        TrackSaveChanges(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void TrackSaveChanges(DbContextEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        _saveChangesCount++;
        QueryDuckTransactionTimeline.RecordSaveChanges(_saveChangesCount);
        if (_saveChangesCount > 1)
        {
            QueryDuckSession.AddWarning(
                $"Repeated SaveChanges detected ({_saveChangesCount} calls this session) — consider batching updates or a single unit-of-work commit.");
            QueryDuckSaveChangesCapture.Record(eventData.Context, _saveChangesCount);
        }
    }
}
