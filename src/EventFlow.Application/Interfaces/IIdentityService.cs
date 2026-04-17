namespace EventFlow.Application.Interfaces;

/// <summary>
/// Abstrai o ASP.NET Core Identity do Application layer.
///
/// POR QUÊ essa camada de abstração?
/// O ASP.NET Core Identity é uma implementação concreta (Infrastructure).
/// Se o Application chamasse UserManager<T> diretamente, teríamos:
/// - Dependência de uma lib de infraestrutura dentro do Application (viola Clean Architecture)
/// - Necessidade de mockar UserManager nos testes (complexo, muitos métodos)
///
/// Com IIdentityService:
/// - Application só conhece a interface
/// - Nos testes: mock simples de IIdentityService
/// - Se trocarmos Identity por Keycloak/Auth0 no futuro: só a Infrastructure muda
/// </summary>
public interface IIdentityService
{
    Task<(bool Success, string[] Errors)> RegisterAsync(
        string name,
        string email,
        string password,
        string role);

    Task<(bool Success, Guid UserId, IList<string> Roles)> LoginAsync(
        string email,
        string password);

    Task<(bool Success, string[] Errors)> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword);

    Task<bool> UserExistsAsync(string email);
}
