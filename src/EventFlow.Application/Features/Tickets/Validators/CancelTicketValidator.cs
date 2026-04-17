using EventFlow.Application.Features.Tickets.Commands;
using FluentValidation;

namespace EventFlow.Application.Features.Tickets.Validators;

public class CancelTicketValidator : AbstractValidator<CancelTicketCommand>
{
    public CancelTicketValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage("O ID do ingresso é obrigatório.");
    }
}
