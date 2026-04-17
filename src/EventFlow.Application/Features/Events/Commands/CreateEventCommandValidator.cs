using FluentValidation;

namespace EventFlow.Application.Features.Events.Commands;

public sealed class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("O título é obrigatório.")
            .MaximumLength(200).WithMessage("O título deve ter no máximo 200 caracteres.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("A descrição é obrigatória.")
            .MaximumLength(2000).WithMessage("A descrição deve ter no máximo 2000 caracteres.");

        RuleFor(x => x.Location)
            .NotEmpty().WithMessage("O local é obrigatório.")
            .MaximumLength(300).WithMessage("O local deve ter no máximo 300 caracteres.");

        RuleFor(x => x.StartsAt)
            .GreaterThan(DateTime.UtcNow).WithMessage("A data de início deve ser no futuro.");

        RuleFor(x => x.EndsAt)
            .GreaterThan(x => x.StartsAt).WithMessage("A data de término deve ser após a data de início.");

        RuleFor(x => x.Capacity)
            .GreaterThan(0).WithMessage("A capacidade deve ser maior que zero.")
            .LessThanOrEqualTo(100_000).WithMessage("A capacidade não pode exceder 100.000.");
    }
}
