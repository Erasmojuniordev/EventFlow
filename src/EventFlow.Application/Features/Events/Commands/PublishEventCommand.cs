using EventFlow.Application.Common;
using EventFlow.Application.Features.Events.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Enums;
using EventFlow.Domain.Interfaces;
using MediatR;

namespace EventFlow.Application.Features.Events.Commands;

public record PublishEventCommand(Guid EventId) : IRequest<Result<EventDto>>;

public sealed class PublishEventCommandHandler(
    IEventRepository eventRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : IRequestHandler<PublishEventCommand, Result<EventDto>>
{
    public async Task<Result<EventDto>> Handle(
        PublishEventCommand command,
        CancellationToken cancellationToken)
    {
        var @event = await eventRepository.GetByIdTrackedAsync(command.EventId, cancellationToken);

        if (@event is null)
            return Result<EventDto>.NotFound($"Evento '{command.EventId}' não encontrado.");

        if (@event.OrganizerId != currentUser.Id && !currentUser.IsInRole(UserRole.Admin))
            return Result<EventDto>.Forbidden("Você não tem permissão para publicar este evento.");

        // A validação de regras (status atual, data no passado) está na entidade:
        // @event.Publish() lança DomainException se inválido.
        // O ExceptionMiddleware captura e retorna 422.
        @event.Publish();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<EventDto>.Success(@event.ToDto());
    }
}
