using EventFlow.Application.Common;
using EventFlow.Application.Features.Tickets.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Interfaces;
using MediatR;

namespace EventFlow.Application.Features.Tickets.Queries;

/// <summary>
/// Retorna todos os ingressos do attendee logado, incluindo o título do evento.
/// </summary>
public record GetMyTicketsQuery : IRequest<Result<IReadOnlyList<TicketDto>>>;

public sealed class GetMyTicketsQueryHandler(
    ITicketRepository ticketRepo,
    IEventRepository eventRepo,
    ICurrentUser currentUser) : IRequestHandler<GetMyTicketsQuery, Result<IReadOnlyList<TicketDto>>>
{
    public async Task<Result<IReadOnlyList<TicketDto>>> Handle(
        GetMyTicketsQuery request,
        CancellationToken cancellationToken)
    {
        var tickets = await ticketRepo.GetByAttendeeIdAsync(currentUser.Id, cancellationToken);

        if (tickets.Count == 0)
            return Result<IReadOnlyList<TicketDto>>.Success(Array.Empty<TicketDto>());

        // Buscar títulos dos eventos em batch — evita N+1 queries
        // POR QUÊ não usar Include() aqui?
        // - O TicketRepository retorna uma lista plana (AsNoTracking)
        // - Adicionar Include no repositório genérico quebraria a separação de responsabilidades
        // - Para este volume, N queries de evento são aceitáveis
        // - Em produção com muitos tickets: considerar um TicketSummaryQueryModel com JOIN
        var eventIds = tickets.Select(t => t.EventId).Distinct().ToList();
        var eventTitles = new Dictionary<Guid, string>();

        foreach (var eventId in eventIds)
        {
            var @event = await eventRepo.GetByIdAsync(eventId, cancellationToken);
            eventTitles[eventId] = @event?.Title ?? "(evento removido)";
        }

        var dtos = tickets
            .Select(t => t.ToDto(eventTitles[t.EventId]))
            .ToList();

        return Result<IReadOnlyList<TicketDto>>.Success(dtos);
    }
}
