using EventFlow.Domain.Entities;

namespace EventFlow.Application.Features.Events.DTOs;

/// <summary>
/// Métodos de extensão para mapear entidades do Domain para DTOs.
///
/// POR QUÊ extension methods ao invés de AutoMapper?
/// AutoMapper:
///   + Menos código repetitivo para mapeamentos simples
///   - "Mágico" — difícil de debugar quando algo mapeia errado
///   - Configuração global que pode ter efeitos colaterais inesperados
///   - Não funciona bem com propriedades privadas (como as nossas entidades têm)
///
/// Extension methods manuais:
///   + Explícito — você vê exatamente o que está sendo mapeado
///   + Fácil de debugar
///   + Funciona com qualquer propriedade
///   - Mais código para escrever
///
/// Para um projeto de aprendizado, mapeamento explícito é melhor:
/// você entende exatamente o que está acontecendo.
/// </summary>
public static class EventMappings
{
    public static EventDto ToDto(this Domain.Entities.Event @event) => new(
        Id: @event.Id,
        Title: @event.Title,
        Description: @event.Description,
        Location: @event.Location,
        StartsAt: @event.StartsAt,
        EndsAt: @event.EndsAt,
        Capacity: @event.Capacity,
        AvailableSpots: @event.AvailableSpots,
        Status: @event.Status.ToString(),
        OrganizerId: @event.OrganizerId,
        CreatedAt: @event.CreatedAt
    );

    public static EventSummaryDto ToSummaryDto(this Domain.Entities.Event @event) => new(
        Id: @event.Id,
        Title: @event.Title,
        Location: @event.Location,
        StartsAt: @event.StartsAt,
        EndsAt: @event.EndsAt,
        AvailableSpots: @event.AvailableSpots,
        Capacity: @event.Capacity,
        Status: @event.Status.ToString()
    );
}
