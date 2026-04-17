using EventFlow.Application.Interfaces;
using EventFlow.Domain.Interfaces;
using EventFlow.Infrastructure.Identity;
using EventFlow.Infrastructure.Interceptors;
using EventFlow.Infrastructure.Persistence;
using EventFlow.Infrastructure.Persistence.Repositories;
using EventFlow.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace EventFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- BANCO DE DADOS ---
        services.AddSingleton<AuditInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' não encontrada. Configure via variável de ambiente.");

            options
                .UseNpgsql(connectionString)
                // Adiciona o interceptor de auditoria
                .AddInterceptors(sp.GetRequiredService<AuditInterceptor>());

            // Em desenvolvimento: loga as queries SQL geradas (útil para aprendizado e debug)
            // Em produção: remover ou usar nível Warning para evitar log excessivo
            // IMPORTANTE: não usar em produção com dados sensíveis — queries podem expor valores
        });

        // --- IDENTITY ---
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            // Configurações de senha — ajustadas para ser realistas sem ser muito restritivas
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;

            // Lockout: bloqueia conta após 5 tentativas falhas por 15 minutos
            // Proteção contra brute force — atacante não consegue testar muitas senhas
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // Email único por usuário
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        // --- AUTENTICAÇÃO JWT ---
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secret = jwtSettings["Secret"]
            ?? throw new InvalidOperationException("JWT Secret não configurado.");

        services.AddAuthentication(options =>
        {
            // Define JWT como esquema padrão de autenticação E desafio
            // Desafio: o que acontece quando uma request não autenticada chega em [Authorize]
            // Com Bearer: retorna 401 automaticamente (sem redirect para login page)
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwtSettings["Audience"],
                ValidateLifetime = true,
                // Tolerância de clock: compensa pequenas diferenças de relógio entre servidores
                // 0 = sem tolerância (mais seguro)
                ClockSkew = TimeSpan.Zero
            };
        });

        // --- REPOSITÓRIOS E UNIT OF WORK ---
        // Scoped: uma instância por request HTTP (correto para repositórios com DbContext)
        // POR QUÊ não Singleton? DbContext não é thread-safe — Singleton causaria problemas
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IWaitlistRepository, WaitlistRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // --- SERVIÇOS ---
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ICurrentUser, CurrentUserService>();
        // Singleton: TokenService não tem estado — mesma instância para todas as requests
        services.AddSingleton<ITokenService, TokenService>();

        // Necessário para IHttpContextAccessor (usado pelo CurrentUserService)
        services.AddHttpContextAccessor();

        return services;
    }
}
