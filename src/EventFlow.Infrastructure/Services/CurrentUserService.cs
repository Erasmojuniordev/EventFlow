using EventFlow.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EventFlow.Infrastructure.Services;

/// <summary>
/// Implementação de ICurrentUser que lê as claims do JWT do HttpContext.
///
/// Claims são informações embutidas no token JWT pelo TokenService no login.
/// Aqui fazemos a operação inversa: extraímos essas claims do token validado.
///
/// COMO FUNCIONA NO PIPELINE DO ASP.NET CORE:
/// 1. Request chega com header "Authorization: Bearer {token}"
/// 2. Middleware JWT valida a assinatura e expiração
/// 3. Se válido, popula HttpContext.User com as claims do token
/// 4. CurrentUserService lê essas claims quando qualquer handler precisar
///
/// POR QUÊ injetar IHttpContextAccessor e não HttpContext diretamente?
/// - HttpContext não pode ser injetado diretamente via DI em serviços com lifetime Scoped/Singleton
/// - IHttpContextAccessor é o accessor thread-safe para o contexto atual
/// - Em background workers (sem HttpContext): IsAuthenticated retorna false graciosamente
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid Id
    {
        get
        {
            var sub = User?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
        }
    }

    public string Email =>
        User?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value
        ?? string.Empty;

    public IEnumerable<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? [];

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
