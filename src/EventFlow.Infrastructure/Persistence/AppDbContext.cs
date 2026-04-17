using EventFlow.Domain.Entities;
using EventFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Infrastructure.Persistence;

/// <summary>
/// DbContext principal da aplicação.
///
/// Herda de IdentityDbContext para integrar as tabelas do Identity (AspNetUsers, Roles, etc.)
/// usando nosso ApplicationUser customizado com Guid como tipo de chave.
///
/// POR QUÊ Guid como tipo de chave do Identity ao invés de string (padrão)?
/// - Consistência: todas as nossas entidades usam Guid
/// - Strings no Identity são GUIDs formatados como string mesmo — melhor usar Guid diretamente
///
/// CONFIGURAÇÃO VIA FLUENT API (IEntityTypeConfiguration<T>):
/// Cada entidade tem sua configuração em arquivo separado em Configurations/.
/// POR QUÊ não usar Data Annotations ([Key], [MaxLength]) nas entidades?
/// - Annotations no Domain criam dependência de EF Core no Domain layer (viola Clean Arch)
/// - Fluent API fica na Infrastructure onde é o lugar correto para detalhes de persistência
/// - Mais expressiva para configurações complexas (índices compostos, value converters, etc.)
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // IMPORTANTE: chamar o base primeiro — configura as tabelas do Identity
        base.OnModelCreating(builder);

        // Aplica automaticamente todas as IEntityTypeConfiguration<T> do assembly
        // ao invés de chamar builder.ApplyConfiguration(new EventConfiguration()) um por um
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
