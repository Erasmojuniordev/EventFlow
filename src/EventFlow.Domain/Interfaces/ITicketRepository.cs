using EventFlow.Domain.Entities;

namespace EventFlow.Domain.Interfaces;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ticket>> GetByAttendeeIdAsync(Guid attendeeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ticket>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se o attendee já tem ingresso ativo (Reserved ou Confirmed) para o evento.
    /// Evita duplicidade: o mesmo usuário não pode ter dois ingressos para o mesmo evento.
    /// </summary>
    Task<bool> HasActiveTicketAsync(Guid eventId, Guid attendeeId, CancellationToken cancellationToken = default);

    Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default);
}
