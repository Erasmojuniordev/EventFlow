using EventFlow.Application.Common;
using EventFlow.Application.Features.Tickets.Notifications;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Events;
using EventFlow.Domain.Interfaces;
using MediatR;

namespace EventFlow.Application.Features.Tickets.Commands;

/// <summary>
/// Cancela um ingresso.
///
/// FLUXO CRÍTICO: dispatch de Domain Events
///
/// Ordem importa:
///   1. Cancelar o ingresso na entidade (acumula TicketCancelled na lista interna)
///   2. SaveChanges — persiste o cancelamento no banco
///   3. SÓ ENTÃO despachar o domain event via MediatR
///
/// POR QUÊ salvar ANTES de despachar?
/// - Se o handler de promoção da fila falhar, o cancelamento já está salvo
/// - Melhor ter: "ingresso cancelado, mas promoção não ocorreu" (pode ser corrigido)
///   do que: "promoção ocorreu, mas cancelamento não foi salvo" (inconsistente)
/// - Em produção real: usar Outbox Pattern para garantia total
///   (salvar o evento na mesma transação que o cancelamento, despachar depois)
///
/// ALTERNATIVA REJEITADA: cancelar e promover na mesma transação
/// - Exigiria que o WaitlistPromotionHandler compartilhasse o mesmo DbContext/transaction
/// - Aumentaria acoplamento e complexidade desnecessária para este projeto
/// </summary>
public record CancelTicketCommand(Guid TicketId) : IRequest<Result<Unit>>;

public sealed class CancelTicketCommandHandler(
    ITicketRepository ticketRepo,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IPublisher publisher) : IRequestHandler<CancelTicketCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(
        CancelTicketCommand request,
        CancellationToken cancellationToken)
    {
        var ticket = await ticketRepo.GetByIdAsync(request.TicketId, cancellationToken);

        if (ticket is null)
            return Result<Unit>.NotFound("Ingresso não encontrado.");

        // Verificação de propriedade do recurso (resource-based authorization)
        // Attendee só pode cancelar o próprio ingresso; Admin pode cancelar qualquer um
        if (ticket.AttendeeId != currentUser.Id &&
            !currentUser.IsInRole(Domain.Enums.UserRole.Admin))
        {
            return Result<Unit>.Forbidden("Você não tem permissão para cancelar este ingresso.");
        }

        // A entidade valida a regra: ingresso Used não pode ser cancelado
        // DomainException é capturada pelo ExceptionMiddleware → 422
        ticket.Cancel();

        // Passo 1: Persistir o cancelamento ANTES de qualquer side effect
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Passo 2: Despachar domain events acumulados na entidade
        // TicketCancelled → WaitlistPromotionHandler será chamado automaticamente pelo MediatR
        foreach (var domainEvent in ticket.DomainEvents)
        {
            if (domainEvent is TicketCancelled cancelled)
            {
                // Convertemos o IDomainEvent (sem referência a MediatR) para INotification (MediatR)
                // Esta é a "ponte" entre Domain e Application
                await publisher.Publish(
                    new TicketCancelledNotification(cancelled.TicketId, cancelled.EventId, cancelled.AttendeeId),
                    cancellationToken);
            }
        }

        ticket.ClearDomainEvents();

        return Result<Unit>.Success(Unit.Value);
    }
}
