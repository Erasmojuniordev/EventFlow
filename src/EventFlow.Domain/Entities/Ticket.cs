using EventFlow.Domain.Enums;
using EventFlow.Domain.Events;
using EventFlow.Domain.Exceptions;

namespace EventFlow.Domain.Entities;

/// <summary>
/// Representa um ingresso para um evento.
///
/// MÁQUINA DE ESTADOS implementada aqui:
///   Reserved → Confirmed → Used
///   Reserved → Cancelled
///   Confirmed → Cancelled
///   Used → (estado terminal, sem transições)
///   Cancelled → (estado terminal, sem transições)
///
/// DOMAIN EVENTS:
/// A entidade não dispara os eventos ela mesma (não tem acesso ao dispatcher).
/// Em vez disso, ela acumula eventos em uma lista (_domainEvents) que o
/// Application handler lê e despacha via MediatR após salvar no banco.
///
/// Por que despachar DEPOIS do SaveChanges?
/// - Garante que o evento só é processado se a transação foi bem-sucedida
/// - Evita "fantasmas": promoção da fila disparada antes do cancelamento ser salvo
/// - Armadilha: se o dispatch falhar, o estado já foi salvo — use Outbox Pattern em produção real
/// </summary>
public class Ticket : BaseEntity
{
    public Guid EventId { get; private set; }
    public Guid AttendeeId { get; private set; }
    public TicketStatus Status { get; private set; }

    // Código único do ingresso — gerado na confirmação, usado para QR Code (Fase 5)
    public string? TicketCode { get; private set; }

    // Acumula domain events para dispatch pelo handler
    // Por quê List<IDomainEvent> e não INotification do MediatR diretamente?
    // - O Domain não referencia MediatR (inversão de dependência)
    // - O Application conhece MediatR e faz a conversão
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Ticket() { }

    public static Ticket Create(Guid eventId, Guid attendeeId)
    {
        return new Ticket
        {
            EventId = eventId,
            AttendeeId = attendeeId,
            Status = TicketStatus.Reserved
        };
    }

    /// <summary>
    /// Confirma o ingresso e gera o código para QR.
    ///
    /// NOTA SOBRE O CÓDIGO: aqui geramos um código simples por enquanto.
    /// Na Fase 5, este código será assinado com HMAC-SHA256 para evitar falsificação.
    /// </summary>
    public void Confirm()
    {
        if (Status != TicketStatus.Reserved)
            throw new DomainException($"Só é possível confirmar um ingresso Reserved. Status atual: '{Status}'.");

        Status = TicketStatus.Confirmed;
        // Código temporário — será substituído por HMAC assinado na Fase 5
        TicketCode = Guid.NewGuid().ToString("N").ToUpperInvariant();
    }

    /// <summary>
    /// Realiza o check-in do ingresso. Estado terminal — não pode ser desfeito.
    /// </summary>
    public void Use()
    {
        if (Status != TicketStatus.Confirmed)
            throw new DomainException($"Só é possível fazer check-in de um ingresso Confirmed. Status atual: '{Status}'.");

        Status = TicketStatus.Used;
    }

    /// <summary>
    /// Cancela o ingresso e emite TicketCancelled para promoção da fila de espera.
    /// </summary>
    public void Cancel()
    {
        if (Status == TicketStatus.Used)
            throw new DomainException("Não é possível cancelar um ingresso já utilizado no check-in.");

        if (Status == TicketStatus.Cancelled)
            throw new DomainException("O ingresso já está cancelado.");

        Status = TicketStatus.Cancelled;

        // Acumula o evento — será despachado pelo handler após salvar no banco
        _domainEvents.Add(new TicketCancelled(Id, EventId, AttendeeId));
    }

    /// <summary>Limpa os eventos após o dispatch para evitar duplo processamento.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
