using EventFlow.Application.Common;
using EventFlow.Application.Features.Tickets.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Interfaces;
using MediatR;

namespace EventFlow.Application.Features.Tickets.Queries;

/// <summary>
/// Retorna a posição do attendee logado na fila de espera de um evento.
/// </summary>
public record GetWaitlistPositionQuery(Guid EventId) : IRequest<Result<WaitlistPositionDto>>;

public sealed class GetWaitlistPositionQueryHandler(
    IWaitlistRepository waitlistRepo,
    ICurrentUser currentUser) : IRequestHandler<GetWaitlistPositionQuery, Result<WaitlistPositionDto>>
{
    public async Task<Result<WaitlistPositionDto>> Handle(
        GetWaitlistPositionQuery request,
        CancellationToken cancellationToken)
    {
        var position = await waitlistRepo.GetPositionAsync(
            request.EventId, currentUser.Id, cancellationToken);

        if (position is null)
            return Result<WaitlistPositionDto>.NotFound("Você não está na lista de espera para este evento.");

        var totalInQueue = await waitlistRepo.GetQueueSizeAsync(request.EventId, cancellationToken);

        return Result<WaitlistPositionDto>.Success(
            new WaitlistPositionDto(request.EventId, position.Value, totalInQueue));
    }
}
