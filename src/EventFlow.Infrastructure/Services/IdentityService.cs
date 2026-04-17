using EventFlow.Application.Interfaces;
using EventFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace EventFlow.Infrastructure.Services;

/// <summary>
/// Implementação de IIdentityService usando ASP.NET Core Identity.
///
/// Este serviço encapsula UserManager e SignInManager do Identity,
/// expondo apenas o que o Application layer precisa através da interface.
/// </summary>
public sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager)
    : IIdentityService
{
    public async Task<(bool Success, string[] Errors)> RegisterAsync(
        string name,
        string email,
        string password,
        string role)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            Name = name,
            EmailConfirmed = true  // Simplificação: sem fluxo de confirmação por email nesta fase
        };

        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description).ToArray());

        // Atribui o role ao usuário
        var roleResult = await userManager.AddToRoleAsync(user, role);

        if (!roleResult.Succeeded)
        {
            // Se falhar o role, desfaz o cadastro — consistência
            await userManager.DeleteAsync(user);
            return (false, roleResult.Errors.Select(e => e.Description).ToArray());
        }

        return (true, Array.Empty<string>());
    }

    public async Task<(bool Success, Guid UserId, IList<string> Roles)> LoginAsync(
        string email,
        string password)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
            return (false, Guid.Empty, Array.Empty<string>());

        // CheckPasswordSignInAsync: verifica senha E aplica lockout se configurado
        // POR QUÊ não usar PasswordHasher.VerifyHashedPassword diretamente?
        // SignInManager gerencia lockout automático (bloqueio após N tentativas falhas)
        // Isso é uma proteção importante contra brute force
        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            password,
            lockoutOnFailure: true);  // conta as tentativas para o lockout

        if (!signInResult.Succeeded)
            return (false, Guid.Empty, Array.Empty<string>());

        var roles = await userManager.GetRolesAsync(user);
        return (true, user.Id, roles);
    }

    public async Task<(bool Success, string[] Errors)> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
            return (false, ["Usuário não encontrado."]);

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);

        return result.Succeeded
            ? (true, Array.Empty<string>())
            : (false, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<bool> UserExistsAsync(string email)
    {
        return await userManager.FindByEmailAsync(email) is not null;
    }
}
