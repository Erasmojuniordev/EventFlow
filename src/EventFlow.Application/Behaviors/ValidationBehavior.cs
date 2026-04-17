using FluentValidation;
using MediatR;

namespace EventFlow.Application.Behaviors;

/// <summary>
/// Pipeline Behavior do MediatR que executa validação antes de qualquer Handler.
///
/// O QUE É UM PIPELINE BEHAVIOR?
/// É um middleware para o MediatR — envolve TODOS os handlers da aplicação.
/// Funciona como um decorator: antes de chamar Handle(), passa pelo behavior.
///
/// Fluxo com este behavior:
///   Controller → MediatR.Send(command)
///     → ValidationBehavior.Handle()   ← validação aqui
///       → LoggingBehavior.Handle()    ← log aqui
///         → Handler.Handle()          ← lógica de negócio aqui
///
/// POR QUÊ validar no pipeline ao invés de em cada handler?
/// - DRY: sem repetir `if (!validator.Validate(cmd).IsValid) throw...` em todo handler
/// - Garantia: impossível esquecer a validação em um handler específico
/// - Separação: o handler foca na lógica de negócio, não em validação de input
///
/// POR QUÊ lançar ValidationException ao invés de retornar Result<T>?
/// - Validação falhou = o caller enviou dados inválidos = erro do client (400)
/// - O ExceptionMiddleware captura ValidationException e converte para 400 ProblemDetails
/// - Alternativa: retornar Result com erro de validação — mais verboso, sem ganho real
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();  // nenhum validator registrado → passa direto

        var context = new ValidationContext<TRequest>(request);

        // Executa todos os validators em paralelo — eficiente quando há múltiplos
        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);  // capturado pelo ExceptionMiddleware

        return await next();
    }
}
