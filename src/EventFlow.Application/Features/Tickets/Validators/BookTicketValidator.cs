using EventFlow.Application.Features.Tickets.Commands;
using FluentValidation;

namespace EventFlow.Application.Features.Tickets.Validators;

public class BookTicketValidator : AbstractValidator<BookTicketCommand>
{
    public BookTicketValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty().WithMessage("O ID do evento é obrigatório.");
    }
}
