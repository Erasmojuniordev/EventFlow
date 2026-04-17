using EventFlow.Application.Common;
using EventFlow.Application.Features.Events.Commands;
using EventFlow.Application.Features.Events.DTOs;
using EventFlow.Application.Features.Events.Queries;
using EventFlow.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventFlow.API.Controllers;

/// <summary>
/// Controller de eventos.
///
/// AUTORIZAÇÃO NESTE CONTROLLER:
///
/// [Authorize] sem parâmetros = qualquer usuário autenticado (qualquer role)
/// [Authorize(Roles = "Organizer")] = só Organizers
/// [Authorize(Roles = "Organizer,Admin")] = Organizer OU Admin
///
/// ROLE-BASED vs RESOURCE-BASED AUTHORIZATION:
///
/// Role-based (feito via atributo):
///   "Só Organizers podem criar eventos" → [Authorize(Roles = "Organizer")]
///   Verificado ANTES de chegar no handler — rápido, sem consultar o banco
///
/// Resource-based (feito no handler):
///   "Só O DONO do evento pode editá-lo" → verificação no handler
///   Precisa carregar o recurso do banco para saber quem é o dono
///   Não dá para fazer via atributo — o atributo não conhece o EventId ainda
///
/// POR QUÊ não usar [Authorize(Policy = "EventOwner")] para resource-based?
/// Policies do ASP.NET Core podem fazer isso via IAuthorizationHandler,
/// mas exigem injetar repositórios dentro de handlers de autorização —
/// mais complexo e menos explícito. Para este projeto, verificação no handler é mais claro.
/// </summary>
[ApiController]
[Route("api/events")]
[Authorize]
public sealed class EventsController(ISender sender) : ControllerBase
{
    /// <summary>Lista eventos publicados com paginação. Acessível para todos os autenticados.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EventSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetEventsQuery(page, pageSize, status, OwnedByCurrentUser: false);
        var result = await sender.Send(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapErrorToResult(result);
    }

    /// <summary>Lista eventos do Organizer logado (inclui Draft e Cancelled).</summary>
    [HttpGet("mine")]
    [Authorize(Roles = UserRole.Organizer)]
    [ProducesResponseType(typeof(PagedResult<EventSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetEventsQuery(page, pageSize, OwnedByCurrentUser: true);
        var result = await sender.Send(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapErrorToResult(result);
    }

    /// <summary>Retorna um evento pelo ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetEventByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapErrorToResult(result);
    }

    /// <summary>Cria um novo evento em rascunho. Só Organizers.</summary>
    [HttpPost]
    [Authorize(Roles = UserRole.Organizer)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEventRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateEventCommand(
            request.Title,
            request.Description,
            request.Location,
            request.StartsAt,
            request.EndsAt,
            request.Capacity);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : MapErrorToResult(result);
    }

    /// <summary>Atualiza um evento em rascunho. Só o Organizer dono ou Admin.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{UserRole.Organizer},{UserRole.Admin}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEventRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateEventCommand(
            id,
            request.Title,
            request.Description,
            request.Location,
            request.StartsAt,
            request.EndsAt,
            request.Capacity);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapErrorToResult(result);
    }

    /// <summary>Publica um evento (Draft → Published). Só o Organizer dono ou Admin.</summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = $"{UserRole.Organizer},{UserRole.Admin}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new PublishEventCommand(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapErrorToResult(result);
    }

    /// <summary>Cancela um evento. Só o Organizer dono ou Admin.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = $"{UserRole.Organizer},{UserRole.Admin}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CancelEventCommand(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapErrorToResult(result);
    }

    // --- Auxiliar centralizado (mesmo padrão do AuthController) ---
    private IActionResult MapErrorToResult<T>(Result<T> result) => result.ErrorType switch
    {
        ResultErrorType.NotFound => NotFound(new ProblemDetails { Title = "Não encontrado", Detail = result.Error, Status = 404 }),
        ResultErrorType.Forbidden => StatusCode(403, new ProblemDetails { Title = "Acesso negado", Detail = result.Error, Status = 403 }),
        ResultErrorType.Conflict => Conflict(new ProblemDetails { Title = "Conflito", Detail = result.Error, Status = 409 }),
        _ => UnprocessableEntity(new ProblemDetails { Title = "Erro de negócio", Detail = result.Error, Status = 422 })
    };
}

// --- Request DTOs ---
public record CreateEventRequest(
    string Title,
    string Description,
    string Location,
    DateTime StartsAt,
    DateTime EndsAt,
    int Capacity);

public record UpdateEventRequest(
    string Title,
    string Description,
    string Location,
    DateTime StartsAt,
    DateTime EndsAt,
    int Capacity);
