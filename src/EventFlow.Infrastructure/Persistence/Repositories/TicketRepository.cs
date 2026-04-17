using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;
using EventFlow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Infrastructure.Persistence.Repositories;

public sealed class TicketRepository(AppDbContext context) : ITicketRepository
{
    public async Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // SEM AsNoTracking — pode ser usado para modificar o status do ticket
        return await context.Tickets
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Ticket>> GetByAttendeeIdAsync(
        Guid attendeeId,
        CancellationToken cancellationToken = default)
    {
        return await context.Tickets
            .AsNoTracking()
            .Where(t => t.AttendeeId == attendeeId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Ticket>> GetByEventIdAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        return await context.Tickets
            .AsNoTracking()
            .Where(t => t.EventId == eventId)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasActiveTicketAsync(
        Guid eventId,
        Guid attendeeId,
        CancellationToken cancellationToken = default)
    {
        return await context.Tickets.AnyAsync(
            t => t.EventId == eventId &&
                 t.AttendeeId == attendeeId &&
                 (t.Status == TicketStatus.Reserved || t.Status == TicketStatus.Confirmed),
            cancellationToken);
    }

    public async Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        await context.Tickets.AddAsync(ticket, cancellationToken);
    }
}
