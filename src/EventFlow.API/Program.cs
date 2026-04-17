using EventFlow.API.Middleware;
using EventFlow.Application;
using EventFlow.Infrastructure;
using EventFlow.Infrastructure.Identity;
using EventFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Threading.RateLimiting;

// --- SERILOG: configuração antecipada para logar erros de startup ---
// Por quê aqui e não no builder.Services?
// Se o app falhar antes de construir o host (ex: connection string inválida),
// o logger já estará configurado para capturar o erro.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Iniciando EventFlow API...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // --- SERILOG: substituindo o logger padrão do .NET ---
    // UseSerilog() integra o Serilog com o ILogger<T> do .NET
    // Todos os _logger.LogInformation() no código usarão o Serilog automaticamente
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)    // lê de appsettings.json
        .ReadFrom.Services(services)                       // permite injetar enrichers via DI
        .Enrich.FromLogContext()                           // inclui propriedades do LogContext (CorrelationId, etc.)
        .Enrich.WithEnvironmentName()                     // útil em produção para distinguir dev/staging/prod
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            path: "logs/eventflow-.log",
            rollingInterval: RollingInterval.Day,           // novo arquivo por dia
            retainedFileCountLimit: 7));                    // mantém últimos 7 dias

    // --- CAMADAS DE SERVIÇOS ---
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // --- CONTROLLERS ---
    builder.Services.AddControllers();

    // --- RATE LIMITING ---
    // Proteção contra brute force no endpoint de login
    // Fixed Window: permite N requests por janela de tempo fixa
    // Alternativa: Sliding Window (janela deslizante, mais suave) ou Token Bucket
    builder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("login", limiterOptions =>
        {
            limiterOptions.PermitLimit = 5;                          // 5 tentativas
            limiterOptions.Window = TimeSpan.FromMinutes(1);         // por minuto
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = 0;                           // sem fila — rejeita imediatamente
        });

        // Resposta quando o rate limit é atingido
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                title = "Muitas tentativas",
                detail = "Limite de tentativas excedido. Aguarde 1 minuto e tente novamente.",
                status = 429
            }, cancellationToken);
        };
    });

    // --- CORS ---
    // Cross-Origin Resource Sharing: permite que o frontend (porta diferente) acesse a API
    // Em produção: substituir pela URL exata do frontend
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("frontend", policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:5173",   // Vite dev server padrão
                    "http://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();  // Necessário para cookies httpOnly (refresh token)
        });
    });

    // --- SWAGGER / OPENAPI ---
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "EventFlow API",
            Version = "v1",
            Description = "API de gerenciamento de eventos e ingressos — projeto de estudo"
        });

        // Configura o Swagger para aceitar o JWT Bearer no header
        var securityScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Insira o JWT token. Exemplo: Bearer {seu_token}"
        };

        options.AddSecurityDefinition("Bearer", securityScheme);
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    // --- HEALTH CHECKS ---
    // Endpoint /health retorna status da API e dependências
    // Usado por Docker, Kubernetes e sistemas de monitoramento
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>("database");

    // =========================================================
    var app = builder.Build();
    // =========================================================

    // --- MIGRATION AUTOMÁTICA EM STARTUP ---
    // Em desenvolvimento: aplica migrations pendentes automaticamente
    // Em produção: migrations devem ser aplicadas separadamente (CI/CD)
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Log.Information("Aplicando migrations...");
        await db.Database.MigrateAsync();

        await SeedRolesAsync(scope.ServiceProvider);
        Log.Information("Migrations e seed concluídos.");
    }

    // --- PIPELINE DE MIDDLEWARES ---
    // A ORDEM IMPORTA MUITO aqui.

    // 1. ExceptionMiddleware: primeiro para capturar qualquer exceção subsequente
    app.UseMiddleware<ExceptionMiddleware>();

    // 2. Correlation ID: antes dos logs para que o ID esteja disponível
    app.UseMiddleware<CorrelationIdMiddleware>();

    // 3. Serilog request logging: loga cada request
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "{RequestMethod} {RequestPath} respondeu {StatusCode} em {Elapsed:0.0000}ms";
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "EventFlow API v1"));
    }

    app.UseHttpsRedirection();
    app.UseCors("frontend");
    app.UseRateLimiter();

    // Autenticação ANTES de autorização — sempre
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    await app.RunAsync();
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Log.Fatal(ex, "EventFlow API encerrada de forma inesperada.");
}
finally
{
    await Log.CloseAndFlushAsync();
}

static async Task SeedRolesAsync(IServiceProvider services)
{
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

    foreach (var roleName in EventFlow.Domain.Enums.UserRole.All)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid> { Name = roleName });
            Log.Information("Role '{RoleName}' criada.", roleName);
        }
    }
}

// Necessário para que o WebApplicationFactory nos testes de integração consiga referenciar
// o assembly desta aplicação. Em .NET 8 com top-level statements, a classe Program é gerada
// internamente — este partial a torna visível para o projeto de testes.
// DEVE ficar após todas as instruções de nível superior e funções locais.
public partial class Program { }
