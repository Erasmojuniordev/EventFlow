using EventFlow.Application.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace EventFlow.Application;

/// <summary>
/// Extension method para registrar todos os serviços do Application layer no DI container.
///
/// PADRÃO ADOTADO: cada camada tem seu próprio DependencyInjection.cs com um
/// método AddApplicationServices() / AddInfrastructureServices() / etc.
///
/// POR QUÊ não registrar tudo no Program.cs?
/// - Program.cs ficaria enorme e acoplado a detalhes de cada camada
/// - Cada camada "sabe" o que precisa registrar — encapsulamento
/// - Fácil de testar: nos testes de integração, chamamos só AddApplicationServices() + mock infra
///
/// O que é Assembly.GetExecutingAssembly()?
/// Retorna o assembly (DLL) atual — neste caso, EventFlow.Application.dll
/// MediatR usa isso para encontrar TODOS os handlers, validators, etc. via reflection
/// ao invés de registrar um por um.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // MediatR: registra todos os IRequestHandler<,> e INotificationHandler<> do assembly
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        // FluentValidation: registra todos os AbstractValidator<T> do assembly
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Pipeline Behaviors — ordem importa: executa na ordem registrada
        // ValidationBehavior → LoggingBehavior → Handler
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        return services;
    }
}
