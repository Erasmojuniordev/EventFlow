using EventFlow.Application.Common;
using EventFlow.Application.Features.Tickets.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Interfaces;
using MediatR;

namespace EventFlow.Application.Features.Tickets.Commands;

/// <summary>
/// Confirma um ingresso reservado, gerando o código para check-in.
///
/// TRANSIÇÃO: Reserved → Confirmed
/// Após confirmado, o attendee recebe um TicketCode que será usado para o QR Code (Fase 5).
///
/// NOTA: O código gerado agora é simples (Guid sem hifens).
/// Na Fase 5, será substituído por HMAC-SHA256 para prevenir falsificação.
/// </summary>
public record ConfirmTicketCommand(Guid TicketId) : IRequest<Result<TicketDto>>;

public sealed class ConfirmTicketCommandHandler(
    ITicketRepository ticketRepo,
    IEventRepository eventRepo,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : IRequestHandler<ConfirmTicketCommand, Result<TicketDto>>
{
    public async Task<Result<TicketDto>> Handle(
        ConfirmTicketCommand request,
        CancellationToken cancellationToken)
    {
        var ticket = await ticketRepo.GetByIdAsync(request.TicketId, cancellationToken);

        if (ticket is null)
            return Result<TicketDto>.NotFound("Ingresso não encontrado.");

        if (ticket.AttendeeId != currentUser.Id &&
            !currentUser.IsInRole(Domain.Enums.UserRole.Admin))
        {
            return Result<TicketDto>.Forbidden("Você não tem permissão para confirmar este ingresso.");
        }

        // Valida a transição Reserved → Confirmed (DomainException se inválida)
        ticket.Confirm();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Buscar o título do evento para montar o DTO
        var @event = await eventRepo.GetByIdAsync(ticket.EventId, cancellationToken);
        var eventTitle = @event?.Title ?? string.Empty;

        return Result<TicketDto>.Success(ticket.ToDto(eventTitle));
    }
}
