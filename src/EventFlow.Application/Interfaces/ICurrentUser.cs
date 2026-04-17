namespace EventFlow.Application.Interfaces;

/// <summary>
/// Provê o contexto do usuário autenticado para o Application layer.
///
/// POR QUÊ uma interface ao invés de injetar IHttpContextAccessor diretamente?
/// - IHttpContextAccessor é infraestrutura HTTP — Application não deveria depender disso
/// - Nos testes de unit: mockar ICurrentUser é trivial; mockar IHttpContextAccessor é verboso
/// - Alternativa ao HttpContext: útil em workers/jobs onde não há contexto HTTP
///
/// A implementação CurrentUserService fica na Infrastructure e lê o ClaimsPrincipal.
/// </summary>
public interface ICurrentUser
{
    Guid Id { get; }
    string Email { get; }
    IEnumerable<string> Roles { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}
