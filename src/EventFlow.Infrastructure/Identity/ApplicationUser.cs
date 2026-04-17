using Microsoft.AspNetCore.Identity;

namespace EventFlow.Infrastructure.Identity;

/// <summary>
/// Extensão do IdentityUser com campos específicos da aplicação.
///
/// POR QUÊ herdar de IdentityUser ao invés de criar do zero?
/// - Identity já implementa: hash de senha, email confirmation, lockout, 2FA, etc.
/// - Reimplementar isso seria perigoso (segurança) e desnecessário
///
/// POR QUÊ não colocar ApplicationUser no Domain?
/// - ApplicationUser herda de IdentityUser, que é uma classe do ASP.NET Core Identity
/// - Isso colocaria uma dependência de infraestrutura dentro do Domain (viola Clean Architecture)
/// - O Domain conhece o conceito de "usuário" apenas pelo Guid Id
///   (ex: Event.OrganizerId, Ticket.AttendeeId)
///
/// ALTERNATIVA DISCUTIDA: criar uma entidade User no Domain separada do ApplicationUser
/// - Mais puro arquiteturalmente, mas adiciona complexidade: precisaria sincronizar os dois
/// - Para este projeto: trade-off aceitável — ApplicationUser na Infrastructure é simples e funcional
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Nome completo do usuário — Identity não tem esse campo por padrão.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Data de cadastro em UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
