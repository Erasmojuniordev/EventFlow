using EventFlow.Domain.Entities;

namespace EventFlow.Domain.Interfaces;

public interface IWaitlistRepository
{
    Task<WaitlistEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna a próxima entrada na fila (menor Position, não promovida).
    /// Usada pelo WaitlistPromotionHandler após um cancelamento.
    /// </summary>
    Task<WaitlistEntry?> GetNextInQueueAsync(Guid eventId, CancellationToken cancellationToken = default);

    Task<int> GetQueueSizeAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna a posição do attendee na fila. Null se não estiver na fila.
    /// </summary>
    Task<int?> GetPositionAsync(Guid eventId, Guid attendeeId, CancellationToken cancellationToken = default);

    Task<bool> IsInQueueAsync(Guid eventId, Guid attendeeId, CancellationToken cancellationToken = default);

    Task AddAsync(WaitlistEntry entry, CancellationToken cancellationToken = default);
}
