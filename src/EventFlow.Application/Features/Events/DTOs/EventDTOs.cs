namespace EventFlow.Application.Features.Events.DTOs;

/// <summary>
/// DTO de resposta para um evento completo (usado em GetById).
/// </summary>
public record EventDto(
    Guid Id,
    string Title,
    string Description,
    string Location,
    DateTime StartsAt,
    DateTime EndsAt,
    int Capacity,
    int AvailableSpots,
    string Status,
    Guid OrganizerId,
    DateTime CreatedAt
);

/// <summary>
/// DTO resumido para listagens (menos dados = menos tráfego).
///
/// POR QUÊ ter dois DTOs diferentes?
/// Na listagem, exibimos cards com título, data e vagas disponíveis.
/// Não precisamos de descrição completa nem organizerId.
/// Retornar menos dados = query mais rápida + response menor.
/// Isso é o princípio de "projection" — selecionar só o que você precisa.
/// </summary>
public record EventSummaryDto(
    Guid Id,
    string Title,
    string Location,
    DateTime StartsAt,
    DateTime EndsAt,
    int AvailableSpots,
    int Capacity,
    string Status
);

/// <summary>
/// Wrapper para respostas paginadas.
///
/// POR QUÊ paginar?
/// Retornar todos os registros de uma vez é inviável com volume alto.
/// Com paginação, o cliente controla quantos registros quer por vez.
///
/// PageNumber: começa em 1 (mais intuitivo para o usuário que para o banco).
/// TotalCount: permite o frontend calcular quantas páginas existem.
/// HasNextPage / HasPreviousPage: evita que o frontend calcule isso.
/// </summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
)
{
    public bool HasNextPage => PageNumber * PageSize < TotalCount;
    public bool HasPreviousPage => PageNumber > 1;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
};
