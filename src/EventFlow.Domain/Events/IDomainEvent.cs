namespace EventFlow.Domain.Events;

/// <summary>
/// Marker interface para Domain Events.
///
/// O QUE É UM DOMAIN EVENT?
/// É um fato que aconteceu no domínio, imutável, no passado.
/// Nomenclatura: sempre no passado — "TicketCancelled", não "CancelTicket".
///
/// POR QUÊ usar Domain Events ao invés de chamar o serviço diretamente?
///
/// Sem Domain Events (acoplamento direto):
///   handler.CancelTicket() {
///       ticket.Cancel();
///       await waitlistService.PromoteNext(ticket.EventId); // ← Application sabe de Waitlist
///       await emailService.SendCancellation(ticket);       // ← Application sabe de Email
///   }
///
/// Com Domain Events (baixo acoplamento):
///   handler.CancelTicket() {
///       ticket.Cancel(); // ← emite TicketCancelled internamente
///   }
///   // Outros handlers ESCUTAM o evento e reagem independentemente
///   WaitlistPromotionHandler.Handle(TicketCancelled e) { ... }
///   CancellationEmailHandler.Handle(TicketCancelled e) { ... }
///
/// VANTAGENS:
/// - Adicionar "enviar email" não requer tocar no CancelTicketHandler
/// - Cada handler tem uma única responsabilidade (SRP)
/// - Facilita testes: testa a lógica de cancelamento separado da lógica de promoção
///
/// DESVANTAGEM / ARMADILHA COMUM:
/// - Domain Events síncronos (dentro da mesma transaction) são simples, mas
///   se a promoção da fila falhar, o cancelamento também falha — nem sempre desejado
/// - Para operações assíncronas (email, notificações), considerar um event bus (fora do escopo aqui)
///
/// IMPLEMENTAÇÃO AQUI: usamos o MediatR como dispatcher de Domain Events
/// — simples e já está no projeto. Em sistemas maiores, considerar Outbox Pattern.
/// </summary>
public interface IDomainEvent { }
