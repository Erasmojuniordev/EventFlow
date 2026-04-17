namespace EventFlow.Domain.Events;

/// <summary>
/// Evento de domínio: um ingresso foi cancelado.
///
/// Quem escuta esse evento?
/// - WaitlistPromotionHandler → promove o próximo da fila de espera
/// (Fase 5: CancellationEmailHandler → notifica o attendee por email)
///
/// Por que registrar EventId e TicketId mas não o Ticket inteiro?
/// - Eventos de domínio devem ser leves — IDs são suficientes para os handlers buscarem o que precisam
/// - Evita problemas de estado stale (o handler sempre busca dados frescos do banco)
/// </summary>
public record TicketCancelled(
    Guid TicketId,
    Guid EventId,
    Guid AttendeeId
) : IDomainEvent;
