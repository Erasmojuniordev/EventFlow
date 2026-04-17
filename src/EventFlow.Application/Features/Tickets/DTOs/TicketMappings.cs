using EventFlow.Domain.Entities;

namespace EventFlow.Application.Features.Tickets.DTOs;

/// <summary>
/// Extensões de mapeamento — converte entidades do Domain para DTOs da Application.
///
/// POR QUÊ métodos de extensão ao invés de AutoMapper?
/// - AutoMapper esconde mapeamentos em configurações separadas — difícil de depurar
/// - Mapeamento explícito é mais fácil de ler e rastrear erros de compilação
/// - Para projetos de aprendizado, a transparência é mais importante que a brevidade
/// </summary>
public static class TicketMappings
{
    public static TicketDto ToDto(this Ticket ticket, string eventTitle) =>
        new(
            Id: ticket.Id,
            EventId: ticket.EventId,
            EventTitle: eventTitle,
            AttendeeId: ticket.AttendeeId,
            Status: ticket.Status.ToString(),
            TicketCode: ticket.TicketCode,
            CreatedAt: ticket.CreatedAt);
}
