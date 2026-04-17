using FluentValidation;

namespace EventFlow.Application.Features.Auth.Commands;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .EmailAddress().WithMessage("Formato de e-mail inválido.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A senha é obrigatória.");
        // Não validamos regras de complexidade aqui: a senha pode ser qualquer coisa no login.
        // Regras de complexidade são só no cadastro.
    }
}
