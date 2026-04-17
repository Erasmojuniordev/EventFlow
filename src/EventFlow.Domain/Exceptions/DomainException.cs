namespace EventFlow.Domain.Exceptions;

/// <summary>
/// Exceção base para violações de regras de negócio no domínio.
///
/// Por que ter uma exceção customizada de domínio?
/// - Distingue erros de negócio (esperados, recoverable) de erros de sistema (bugs, infra)
/// - O ExceptionMiddleware na API pode tratá-la diferente:
///   DomainException → 400 Bad Request (erro do usuário/client)
///   Exception genérica → 500 Internal Server Error (bug nosso)
///
/// Por que NÃO usar exceptions para fluxo de controle normal?
/// - Exceptions são caras computacionalmente (stack unwinding)
/// - Semântica: exception = situação EXCEPCIONAL, não regra de negócio falhando
/// - Em fluxos como "ticket não encontrado", usamos Result<T> pattern no Application layer
///
/// QUANDO usar DomainException então?
/// - Invariantes do domínio que NUNCA deveriam ser violadas por código correto
/// - Ex: tentar cancelar um ingresso já Used — isso é bug no caller, não input inválido do usuário
/// - Validação de input do usuário fica no FluentValidation, não aqui
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }

    public DomainException(string message, Exception innerException)
        : base(message, innerException) { }
}
