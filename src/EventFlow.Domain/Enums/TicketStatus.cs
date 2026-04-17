namespace EventFlow.Domain.Enums;

/// <summary>
/// Status do ciclo de vida de um ingresso.
///
/// Máquina de estados do Ticket:
///
///   Reserved ──► Confirmed ──► Used
///      │              │
///      └──────────────┴──► Cancelled
///
/// Reserved:  Ingresso reservado mas não confirmado (ex: pagamento pendente).
///            Neste projeto simplificamos: a reserva já confirma automaticamente
///            se há vaga disponível. Mantemos o estado para didática de fluxo real.
///
/// Confirmed: Vaga garantida. Ingresso válido para check-in. QR Code gerado.
///
/// Used:      Check-in realizado. Estado terminal — não pode mais ser cancelado.
///            Por quê? Segurança: evitar cancelar para reembolso após usar o evento.
///
/// Cancelled: Reserva ou ingresso cancelado. Libera a vaga, dispara promoção da fila de espera.
///            Estado terminal — não se pode reativar um ingresso cancelado.
///            Por quê terminal? Auditoria: manter histórico de cancelamentos.
/// </summary>
public enum TicketStatus
{
    Reserved = 0,
    Confirmed = 1,
    Used = 2,
    Cancelled = 3
}
