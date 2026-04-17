using EventFlow.Application.Common;
using EventFlow.Application.Features.Events.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Enums;
using EventFlow.Domain.Interfaces;
using MediatR;

namespace EventFlow.Application.Features.Events.Commands;

public record CancelEventCommand(Guid EventId) : IRequest<Result<EventDto>>;

public sealed class CancelEventCommandHandler(
    IEventRepository eventRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CancelEventCommand, Result<EventDto>>
{
    public async Task<Result<EventDto>> Handle(
        CancelEventCommand command,
        CancellationToken cancellationToken)
    {
        // GetByIdWithTicketsAsync: precisamos dos tickets para cancelá-los também
        var @event = await eventRepository.GetByIdWithTicketsAsync(command.EventId, cancellationToken);

        if (@event is null)
            return Result<EventDto>.NotFound($"Evento '{command.EventId}' não encontrado.");

        if (@event.OrganizerId != currentUser.Id && !currentUser.IsInRole(UserRole.Admin))
            return Result<EventDto>.Forbidden("Você não tem permissão para cancelar este evento.");

        // Cancela o evento (valida a máquina de estados internamente)
        @event.Cancel();

        // Cancela todos os ingressos ativos do evento.
        // POR QUÊ aqui e não dentro de Event.Cancel()?
        // - A entidade Event não deveria emitir eventos em nome dos Tickets
        // - O handler tem visão do agregado completo e pode coordenar os dois
        // - Em sistemas mais complexos, isso seria um Domain Service
        foreach (var ticket in @event.Tickets.Where(t =>
            t.Status == Domain.Enums.TicketStatus.Reserved ||
            t.Status == Domain.Enums.TicketStatus.Confirmed))
        {
            ticket.Cancel();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // TODO Fase 3: despachar Domain Events acumulados nos tickets
        // (promoção da fila de espera e notificações)

        return Result<EventDto>.Success(@event.ToDto());
    }
}
