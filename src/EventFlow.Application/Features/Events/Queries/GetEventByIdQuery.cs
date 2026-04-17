using EventFlow.Application.Common;
using EventFlow.Application.Features.Events.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Enums;
using EventFlow.Domain.Interfaces;
using MediatR;

namespace EventFlow.Application.Features.Events.Queries;

public record GetEventByIdQuery(Guid EventId) : IRequest<Result<EventDto>>;

public sealed class GetEventByIdQueryHandler(
    IEventRepository eventRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetEventByIdQuery, Result<EventDto>>
{
    public async Task<Result<EventDto>> Handle(
        GetEventByIdQuery query,
        CancellationToken cancellationToken)
    {
        var @event = await eventRepository.GetByIdAsync(query.EventId, cancellationToken);

        if (@event is null)
            return Result<EventDto>.NotFound($"Evento '{query.EventId}' não encontrado.");

        // Attendees só podem ver eventos Published
        // Organizers e Admins podem ver qualquer status (para gerenciamento)
        var canSeeNonPublished =
            currentUser.IsInRole(UserRole.Admin) ||
            (currentUser.IsInRole(UserRole.Organizer) && @event.OrganizerId == currentUser.Id);

        if (@event.Status != Domain.Enums.EventStatus.Published && !canSeeNonPublished)
            return Result<EventDto>.NotFound($"Evento '{query.EventId}' não encontrado.");
        // Retornamos NotFound ao invés de Forbidden propositalmente:
        // não queremos revelar que o evento existe mas está em Draft
        // (information disclosure — mesma lógica do "credenciais inválidas" no login)

        return Result<EventDto>.Success(@event.ToDto());
    }
}
