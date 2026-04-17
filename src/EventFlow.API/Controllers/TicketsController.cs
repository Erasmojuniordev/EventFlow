using EventFlow.Application.Common;
using EventFlow.Application.Features.Tickets.Commands;
using EventFlow.Application.Features.Tickets.DTOs;
using EventFlow.Application.Features.Tickets.Queries;
using EventFlow.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventFlow.API.Controllers;

/// <summary>
/// Controller de ingressos e lista de espera.
///
/// DESIGN DE ROTAS:
/// - POST /api/events/{eventId}/tickets → reservar ingresso (ingresso pertence ao evento)
/// - POST /api/tickets/{id}/confirm    → confirmar ingresso específico
/// - DELETE /api/tickets/{id}          → cancelar ingresso específico
/// - GET /api/tickets/mine             → ingressos do attendee logado
/// - GET /api/events/{eventId}/waitlist/position → posição na fila do evento
///
/// POR QUÊ misturar /events/ e /tickets/ nas rotas?
/// - Booking: faz sentido como sub-recurso de evento ("/events/{id}/tickets")
/// - Operações no ingresso: o ingresso é o recurso principal ("/tickets/{id}")
/// - Isso é REST Resource-Oriented Design — cada rota reflete a "ownership" do recurso
/// </summary>
[ApiController]
[Authorize]
public sealed class TicketsController(ISender sender) : ControllerBase
{
    /// <summary>Reserva um ingresso para o evento. Se lotado, entra na lista de espera.</summary>
    [HttpPost("api/events/{eventId:guid}/tickets")]
    [Authorize(Roles = UserRole.Attendee)]
    [ProducesResponseType(typeof(BookTicketResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BookTicket(Guid eventId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new BookTicketCommand(eventId), cancellationToken);

        if (!result.IsSuccess)
            return MapErrorToResult(result);

        // 201 Created quando ingresso foi criado; 200 OK quando entrou na fila de espera
        return result.Value!.IsTicket
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : Ok(result.Value);
    }

    /// <summary>Confirma um ingresso reservado (Reserved → Confirmed). Gera o TicketCode para QR.</summary>
    [HttpPost("api/tickets/{id:guid}/confirm")]
    [Authorize(Roles = UserRole.Attendee)]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmTicket(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ConfirmTicketCommand(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapErrorToResult(result);
    }

    /// <summary>Cancela um ingresso. Attendee cancela o próprio; Admin pode cancelar qualquer um.</summary>
    [HttpDelete("api/tickets/{id:guid}")]
    [Authorize(Roles = $"{UserRole.Attendee},{UserRole.Admin}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CancelTicket(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CancelTicketCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : MapErrorToResult(result);
    }

    /// <summary>Lista todos os ingressos do attendee logado.</summary>
    [HttpGet("api/tickets/mine")]
    [Authorize(Roles = UserRole.Attendee)]
    [ProducesResponseType(typeof(IReadOnlyList<TicketDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTickets(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyTicketsQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapErrorToResult(result);
    }

    /// <summary>Retorna a posição do attendee logado na fila de espera do evento.</summary>
    [HttpGet("api/events/{eventId:guid}/waitlist/position")]
    [Authorize(Roles = UserRole.Attendee)]
    [ProducesResponseType(typeof(WaitlistPositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWaitlistPosition(Guid eventId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetWaitlistPositionQuery(eventId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapErrorToResult(result);
    }

    private IActionResult MapErrorToResult<T>(Result<T> result) => result.ErrorType switch
    {
        ResultErrorType.NotFound => NotFound(new ProblemDetails { Title = "Não encontrado", Detail = result.Error, Status = 404 }),
        ResultErrorType.Forbidden => StatusCode(403, new ProblemDetails { Title = "Acesso negado", Detail = result.Error, Status = 403 }),
        ResultErrorType.Conflict => Conflict(new ProblemDetails { Title = "Conflito", Detail = result.Error, Status = 409 }),
        _ => UnprocessableEntity(new ProblemDetails { Title = "Erro de negócio", Detail = result.Error, Status = 422 })
    };
}
