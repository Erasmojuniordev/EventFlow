using EventFlow.Application.Common;
using EventFlow.Application.Features.Events.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Enums;
using EventFlow.Domain.Interfaces;
using MediatR;

namespace EventFlow.Application.Features.Events.Queries;

/// <summary>
/// Query para listar eventos com paginação e filtros.
///
/// DESIGN: a Query carrega os filtros como parâmetros opcionais.
/// Valores null = sem filtro para aquele campo.
///
/// POR QUÊ não ter uma Query separada para cada combinação de filtro?
/// - Explodiria o número de classes
/// - A mesma lógica de paginação seria repetida
/// - Um único handler com filtros condicionais é mais simples aqui
///
/// OwnedByCurrentUser: quando true, retorna só os eventos do Organizer logado.
/// Usado na tela "Meus Eventos" do Organizer.
/// </summary>
public record GetEventsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Status = null,
    bool OwnedByCurrentUser = false
) : IRequest<Result<PagedResult<EventSummaryDto>>>;

public sealed class GetEventsQueryHandler(
    IEventRepository eventRepository,
    ICurrentUser currentUser)
    : IRequestHandler<GetEventsQuery, Result<PagedResult<EventSummaryDto>>>
{
    public async Task<Result<PagedResult<EventSummaryDto>>> Handle(
        GetEventsQuery query,
        CancellationToken cancellationToken)
    {
        // Busca os eventos conforme o contexto do usuário
        var events = query.OwnedByCurrentUser
            ? await eventRepository.GetByOrganizerIdAsync(currentUser.Id, cancellationToken)
            : await eventRepository.GetAllPublishedAsync(cancellationToken);

        // Filtro de status em memória (lista já é pequena após o filtro do banco)
        // Em volume alto: mover o filtro para dentro do repositório (query no banco)
        var filtered = events.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<EventStatus>(query.Status, ignoreCase: true, out var statusEnum))
        {
            filtered = filtered.Where(e => e.Status == statusEnum);
        }

        var totalCount = filtered.Count();

        // Paginação: Skip pula os itens das páginas anteriores
        // PageNumber=1, PageSize=10: Skip(0).Take(10) → itens 1-10
        // PageNumber=2, PageSize=10: Skip(10).Take(10) → itens 11-20
        var items = filtered
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => e.ToSummaryDto())
            .ToList();

        var result = new PagedResult<EventSummaryDto>(
            Items: items,
            TotalCount: totalCount,
            PageNumber: query.PageNumber,
            PageSize: query.PageSize);

        return Result<PagedResult<EventSummaryDto>>.Success(result);
    }
}
