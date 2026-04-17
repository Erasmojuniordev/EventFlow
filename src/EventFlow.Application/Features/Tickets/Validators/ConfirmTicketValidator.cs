using EventFlow.Application.Features.Tickets.Commands;
using FluentValidation;

namespace EventFlow.Application.Features.Tickets.Validators;

public class ConfirmTicketValidator : AbstractValidator<ConfirmTicketCommand>
{
    public ConfirmTicketValidator()
    {
        RuleFor(x => x.TicketId)
            .NotEmpty().WithMessage("O ID do ingresso é obrigatório.");
    }
}
