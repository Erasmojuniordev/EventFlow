using EventFlow.Domain.Exceptions;

namespace EventFlow.Domain.Entities;

/// <summary>
/// Representa uma entrada na fila de espera para um evento lotado.
///
/// COMO FUNCIONA A FILA:
/// 1. Attendee tenta reservar ingresso → evento está cheio → cria WaitlistEntry
/// 2. Outro Attendee cancela ingresso → TicketCancelled é disparado
/// 3. WaitlistPromotionHandler busca a entrada com menor Position → cria Ticket para ela
/// 4. WaitlistEntry é marcada como Promoted
///
/// POR QUÊ guardar Position ao invés de usar CreatedAt para ordenar?
/// - CreatedAt pode ter colisões em sistemas com alta concorrência
/// - Position é atribuída explicitamente, garantindo ordem determinística
/// - Permite reordenação manual pelo organizador (feature futura)
///
/// ALTERNATIVA: usar uma fila (Queue) real (Redis, Service Bus).
/// Para este projeto, PostgreSQL + índice em (EventId, Position) é suficiente.
/// Em produção com milhares de entradas por segundo, uma fila dedicada seria melhor.
/// </summary>
public class WaitlistEntry : BaseEntity
{
    public Guid EventId { get; private set; }
    public Guid AttendeeId { get; private set; }
    public int Position { get; private set; }
    public bool IsPromoted { get; private set; }

    private WaitlistEntry() { }

    public static WaitlistEntry Create(Guid eventId, Guid attendeeId, int position)
    {
        if (position < 1)
            throw new DomainException("A posição na fila deve ser maior que zero.");

        return new WaitlistEntry
        {
            EventId = eventId,
            AttendeeId = attendeeId,
            Position = position,
            IsPromoted = false
        };
    }

    /// <summary>
    /// Marca esta entrada como promovida. Um Ticket foi criado para este attendee.
    /// </summary>
    public void Promote()
    {
        if (IsPromoted)
            throw new DomainException("Esta entrada na fila já foi promovida.");

        IsPromoted = true;
    }
}
