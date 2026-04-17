using EventFlow.Application.Common;
using EventFlow.Application.Features.Events.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Enums;
using EventFlow.Domain.Interfaces;
using MediatR;

namespace EventFlow.Application.Features.Events.Commands;

public record UpdateEventCommand(
    Guid EventId,
    string Title,
    string Description,
    string Location,
    DateTime StartsAt,
    DateTime EndsAt,
    int Capacity
) : IRequest<Result<EventDto>>;

public sealed class UpdateEventCommandHandler(
    IEventRepository eventRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateEventCommand, Result<EventDto>>
{
    public async Task<Result<EventDto>> Handle(
        UpdateEventCommand command,
        CancellationToken cancellationToken)
    {
        // GetByIdWithTicketsAsync: retorna tracked para modificação
        var @event = await eventRepository.GetByIdTrackedAsync(command.EventId, cancellationToken);

        if (@event is null)
            return Result<EventDto>.NotFound($"Evento '{command.EventId}' não encontrado.");

        // RESOURCE-BASED AUTHORIZATION:
        // Verifica se o usuário logado é o dono do evento.
        // Fazemos isso no handler e não só no [Authorize] porque:
        // [Authorize(Roles = "Organizer")] garante que é um Organizer,
        // mas NÃO garante que é O Organizer DESTE evento.
        // Um Organizer não pode editar o evento de outro Organizer.
        if (@event.OrganizerId != currentUser.Id && !currentUser.IsInRole(UserRole.Admin))
            return Result<EventDto>.Forbidden("Você não tem permissão para editar este evento.");

        @event.Update(
            command.Title,
            command.Description,
            command.Location,
            command.StartsAt,
            command.EndsAt,
            command.Capacity);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<EventDto>.Success(@event.ToDto());
    }
}
