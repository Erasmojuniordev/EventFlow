using MediatR;

namespace EventFlow.Application.Features.Tickets.Notifications;

/// <summary>
/// Notificação MediatR disparada quando um ingresso é cancelado.
///
/// POR QUÊ criar esta classe se já existe TicketCancelled no Domain?
///
/// O Domain define: TicketCancelled : IDomainEvent (interface sem dependência de MediatR)
/// O Application define: TicketCancelledNotification : INotification (interface do MediatR)
///
/// Esse é o padrão "Domain Event → Application Notification":
/// - Mantém o Domain com zero dependências externas (Clean Architecture)
/// - O handler do command faz a conversão: TicketCancelled → TicketCancelledNotification
/// - Múltiplos INotificationHandler podem responder ao mesmo evento (extensível)
///
/// ALTERNATIVA: fazer TicketCancelled implementar INotification diretamente
///   → Quebraria a regra "Domain tem zero dependências" (precisaria referenciar MediatR)
///   → Não vale a redução de código
/// </summary>
public record TicketCancelledNotification(
    Guid TicketId,
    Guid EventId,
    Guid AttendeeId) : INotification;
