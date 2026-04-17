using EventFlow.Domain.Entities;
using EventFlow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Infrastructure.Persistence.Repositories;

public sealed class WaitlistRepository(AppDbContext context) : IWaitlistRepository
{
    public async Task<WaitlistEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.WaitlistEntries
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<WaitlistEntry?> GetNextInQueueAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        // Busca a entrada com menor Position que ainda não foi promovida
        // O índice ix_waitlist_event_position garante que essa query é eficiente
        return await context.WaitlistEntries
            .Where(w => w.EventId == eventId && !w.IsPromoted)
            .OrderBy(w => w.Position)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> GetQueueSizeAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await context.WaitlistEntries
            .CountAsync(w => w.EventId == eventId && !w.IsPromoted, cancellationToken);
    }

    public async Task<int?> GetPositionAsync(
        Guid eventId,
        Guid attendeeId,
        CancellationToken cancellationToken = default)
    {
        var entry = await context.WaitlistEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                w => w.EventId == eventId && w.AttendeeId == attendeeId && !w.IsPromoted,
                cancellationToken);

        return entry?.Position;
    }

    public async Task<bool> IsInQueueAsync(
        Guid eventId,
        Guid attendeeId,
        CancellationToken cancellationToken = default)
    {
        return await context.WaitlistEntries.AnyAsync(
            w => w.EventId == eventId && w.AttendeeId == attendeeId && !w.IsPromoted,
            cancellationToken);
    }

    public async Task AddAsync(WaitlistEntry entry, CancellationToken cancellationToken = default)
    {
        await context.WaitlistEntries.AddAsync(entry, cancellationToken);
    }
}
