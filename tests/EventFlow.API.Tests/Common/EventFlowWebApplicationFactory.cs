using EventFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace EventFlow.API.Tests.Common;

/// <summary>
/// Factory customizada para testes de integração com banco de dados REAL.
///
/// POR QUÊ TestContainers e não SQLite ou InMemory?
///
/// PROBLEMA COM SQLite/InMemory:
/// - SQLite não suporta todas as features do PostgreSQL (xmin, tipos específicos)
/// - InMemory ignora restrições de chave estrangeira e índices únicos
/// - Resultado: testes que passam em SQLite falham em produção com PostgreSQL
///
/// COM TestContainers:
/// - Sobe um container PostgreSQL real antes dos testes
/// - Testa exatamente o mesmo banco que será usado em produção
/// - Detecta problemas reais: índices, constraints, comportamento de xmin
/// - Desvantagem: mais lento (~5-15s para subir o container) e requer Docker
///
/// COMO FUNCIONA:
/// 1. TestContainers sobe um container PostgreSQL em uma porta aleatória
/// 2. WebApplicationFactory substitui a connection string da aplicação
/// 3. EF Core aplica as migrations no banco de teste
/// 4. Testes rodam contra o banco real
/// 5. Container é destruído após todos os testes da classe
///
/// ISOLAMENTO:
/// Cada classe de teste que usa IClassFixture<EventFlowWebApplicationFactory>
/// compartilha UMA instância desta factory. Dentro de cada teste,
/// chamamos ResetDatabaseAsync() para limpar os dados entre testes.
/// </summary>
public class EventFlowWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // TestContainers cria um container PostgreSQL real em porta aleatória
    // PostgreSqlBuilder configura a imagem, usuário, senha e banco
    // Testcontainers 4.11+: passar a imagem diretamente ao construtor (parameterless foi depreciado)
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("eventflow_tests")
        .WithUsername("postgres")
        .WithPassword("postgres_test")
        .Build();

    /// <summary>
    /// Sobrescreve a configuração da aplicação para apontar para o banco de testes.
    ///
    /// POR QUÊ ConfigureWebHost e não ConfigureServices?
    /// ConfigureWebHost tem acesso ao IWebHostEnvironment, útil para setar
    /// o ambiente como "Testing" e desabilitar comportamentos de Development.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Definir ambiente como Testing para evitar que o Program.cs
        // tente fazer MigrateAsync() automaticamente (nós controlamos isso)
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove o DbContext registrado pela aplicação real
            // (que aponta para o banco de desenvolvimento)
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (descriptor is not null)
                services.Remove(descriptor);

            // Registra um novo DbContext apontando para o banco do TestContainers
            // O container já está rodando neste momento (InitializeAsync foi chamado antes)
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_dbContainer.GetConnectionString()));
        });
    }

    /// <summary>
    /// Chamado ANTES de qualquer teste da classe (IAsyncLifetime).
    /// Inicia o container e aplica as migrations.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Sobe o container PostgreSQL — isso leva alguns segundos na primeira vez
        await _dbContainer.StartAsync();

        // Aplica migrations no banco de testes para criar as tabelas
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // Seed de roles necessárias para os testes de autorização
        await SeedRolesAsync(scope.ServiceProvider);
    }

    /// <summary>
    /// Chamado DEPOIS de todos os testes da classe (IAsyncLifetime).
    /// Para e destrói o container PostgreSQL.
    /// </summary>
    public new async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Limpa os dados das tabelas entre testes para garantir isolamento.
    ///
    /// POR QUÊ TRUNCATE e não recriar o banco?
    /// - Recriar seria muito mais lento (DROP/CREATE/MIGRATE a cada teste)
    /// - TRUNCATE reseta os dados mas mantém a estrutura — rápido
    /// - RESTART IDENTITY reseta sequences (se usassemos int auto-increment)
    /// - CASCADE para respeitar as foreign keys
    ///
    /// ORDEM IMPORTA: truncar na ordem inversa das dependências FK
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Trunca em ordem inversa das dependências de FK
        await db.Database.ExecuteSqlRawAsync(@"
            TRUNCATE TABLE
                waitlist_entries,
                tickets,
                events,
                ""AspNetUserRoles"",
                ""AspNetUserClaims"",
                ""AspNetUserLogins"",
                ""AspNetUserTokens"",
                ""AspNetUsers"",
                refresh_tokens
            RESTART IDENTITY CASCADE;
        ");

        // Re-seed de roles após truncar (AspNetRoles é mantido separadamente)
        await SeedRolesAsync(scope.ServiceProvider);
    }

    private static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>>();

        foreach (var roleName in EventFlow.Domain.Enums.UserRole.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole<Guid> { Name = roleName });
        }
    }
}
