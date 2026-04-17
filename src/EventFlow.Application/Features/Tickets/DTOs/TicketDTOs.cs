namespace EventFlow.Application.Features.Tickets.DTOs;

/// <summary>
/// DTO principal de um ingresso — retornado em todas as respostas de tickets.
/// </summary>
public record TicketDto(
    Guid Id,
    Guid EventId,
    string EventTitle,
    Guid AttendeeId,
    string Status,
    string? TicketCode,
    DateTime CreatedAt);

/// <summary>
/// Resposta do BookTicket — pode ser um ingresso OU uma posição na fila.
///
/// POR QUÊ um único DTO ao invés de dois endpoints diferentes?
/// - O attendee faz a mesma ação ("quero um ingresso") — o sistema decide o resultado
/// - Retornar tipos diferentes exigiria dois endpoints, mas a intenção do usuário é a mesma
/// - O campo IsTicket permite ao frontend decidir o que exibir sem checar null
/// - Quando IsTicket=true: Ticket != null, WaitlistPosition == null
/// - Quando IsTicket=false: Ticket == null, WaitlistPosition != null
/// </summary>
public record BookTicketResponse(
    bool IsTicket,
    TicketDto? Ticket,
    WaitlistPositionDto? WaitlistPosition);

/// <summary>
/// Posição do attendee na fila de espera de um evento.
/// </summary>
public record WaitlistPositionDto(
    Guid EventId,
    int Position,
    int TotalInQueue);
