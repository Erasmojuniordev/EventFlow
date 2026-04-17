namespace EventFlow.Domain.Events;

/// <summary>
/// Evento de domínio: um usuário na fila de espera foi promovido a ingresso confirmado.
///
/// Disparado pelo WaitlistPromotionHandler após processar um TicketCancelled.
///
/// Quem escuta esse evento?
/// (Fase 5: PromotionEmailHandler → notifica o usuário que saiu da fila)
/// </summary>
public record WaitlistEntryPromoted(
    Guid WaitlistEntryId,
    Guid EventId,
    Guid AttendeeId,
    Guid NewTicketId
) : IDomainEvent;
