using EventFlow.Application.Common;
using EventFlow.Application.Features.Events.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Entities;
using EventFlow.Domain.Interfaces;
using MediatR;

namespace EventFlow.Application.Features.Events.Commands;

public record CreateEventCommand(
    string Title,
    string Description,
    string Location,
    DateTime StartsAt,
    DateTime EndsAt,
    int Capacity
) : IRequest<Result<EventDto>>;

public sealed class CreateEventCommandHandler(
    IEventRepository eventRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateEventCommand, Result<EventDto>>
{
    public async Task<Result<EventDto>> Handle(
        CreateEventCommand command,
        CancellationToken cancellationToken)
    {
        var @event = Event.Create(
            command.Title,
            command.Description,
            command.Location,
            command.StartsAt,
            command.EndsAt,
            command.Capacity,
            currentUser.Id);

        await eventRepository.AddAsync(@event, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<EventDto>.Success(@event.ToDto());
    }
}
