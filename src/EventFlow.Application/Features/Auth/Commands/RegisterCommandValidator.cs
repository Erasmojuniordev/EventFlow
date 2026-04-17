using EventFlow.Domain.Enums;
using FluentValidation;

namespace EventFlow.Application.Features.Auth.Commands;

/// <summary>
/// Validador do RegisterCommand — executa ANTES do handler via ValidationBehavior.
///
/// RESPONSABILIDADE: validar INPUT do usuário (não regras de negócio).
/// Exemplos de cada tipo:
///   Input validation (aqui): email válido, senha tem mínimo de 8 chars, role existe
///   Business rule (no handler/domain): email já cadastrado, role "Admin" não pode se auto-registrar
///
/// POR QUÊ FluentValidation ao invés de DataAnnotations ([Required], [EmailAddress])?
/// - DataAnnotations misturam concern de validação com o modelo (acoplamento)
/// - FluentValidation: validação em classe separada, testável isoladamente
/// - Suporte nativo a async (verificar email no banco, por exemplo)
/// - Mensagens de erro personalizadas por campo
/// - Regras condicionais (When, Unless)
/// </summary>
public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .EmailAddress().WithMessage("O e-mail informado não é válido.")
            .MaximumLength(254).WithMessage("E-mail muito longo."); // RFC 5321

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A senha é obrigatória.")
            .MinimumLength(8).WithMessage("A senha deve ter no mínimo 8 caracteres.")
            .Matches("[A-Z]").WithMessage("A senha deve conter ao menos uma letra maiúscula.")
            .Matches("[0-9]").WithMessage("A senha deve conter ao menos um número.");
        // Nota: o Identity também valida senha com suas próprias regras.
        // Validamos aqui para dar erro antes de chegar ao Identity (feedback mais rápido).

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("O perfil (role) é obrigatório.")
            .Must(role => role == UserRole.Organizer || role == UserRole.Attendee)
            .WithMessage($"Perfil inválido. Valores aceitos: '{UserRole.Organizer}', '{UserRole.Attendee}'.");
        // "Admin" é bloqueado aqui — só pode ser criado via seed/migration.
    }
}
