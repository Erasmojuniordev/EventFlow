using EventFlow.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace EventFlow.API.Middleware;

/// <summary>
/// Middleware global de tratamento de exceções.
///
/// O QUE É UM MIDDLEWARE?
/// É um componente no pipeline de request do ASP.NET Core.
/// Cada request passa por uma cadeia de middlewares antes de chegar ao controller:
///   Request → ExceptionMiddleware → CorrelationIdMiddleware → Auth → Controller
///
/// POR QUÊ middleware para exceções ao invés de try/catch em cada controller?
/// - DRY: um único lugar trata todos os erros
/// - Garantia: nenhum controller pode "esquecer" de tratar uma exception
/// - Separação: controllers ficam limpos, sem try/catch
///
/// POR QUÊ ProblemDetails (RFC 7807)?
/// É um padrão HTTP para respostas de erro em APIs JSON.
/// Sem padrão: cada API inventa seu próprio formato de erro:
///   { "error": "Not found" }  ou  { "message": "Not found" }  ou  { "msg": "Not found" }
/// Com ProblemDetails:
///   { "type": "...", "title": "Not Found", "status": 404, "detail": "...", "traceId": "..." }
/// Clientes (frontend, apps mobile) podem tratar de forma genérica.
///
/// O traceId permite correlacionar o erro com os logs no servidor.
/// </summary>
public sealed class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            // FluentValidation: input inválido do usuário → 400 Bad Request
            logger.LogWarning("Validação falhou: {Errors}",
                string.Join("; ", ex.Errors.Select(e => e.ErrorMessage)));

            await WriteProblemDetailsAsync(context, StatusCodes.Status400BadRequest,
                "Dados inválidos",
                "Um ou mais campos de entrada estão inválidos.",
                ex.Errors.ToDictionary(
                    e => e.PropertyName,
                    e => new[] { e.ErrorMessage }));
        }
        catch (DomainException ex)
        {
            // Violação de regra de negócio → 422 Unprocessable Entity
            // 422 é mais semântico que 400 para "dados válidos mas regra de negócio proíbe"
            logger.LogWarning("DomainException: {Message}", ex.Message);

            await WriteProblemDetailsAsync(context, StatusCodes.Status422UnprocessableEntity,
                "Regra de negócio violada",
                ex.Message);
        }
        catch (Exception ex)
        {
            // Erro inesperado → 500
            // SEGURANÇA: nunca expor stack trace ou mensagem interna ao client
            // A mensagem genérica protege contra information disclosure
            logger.LogError(ex, "Erro interno não tratado");

            await WriteProblemDetailsAsync(context, StatusCodes.Status500InternalServerError,
                "Erro interno",
                "Ocorreu um erro inesperado. Consulte os logs com o traceId para diagnóstico.");
        }
    }

    private static async Task WriteProblemDetailsAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        Dictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            // TraceId: ligado ao CorrelationId da request — permite rastrear nos logs
            Extensions = { ["traceId"] = context.TraceIdentifier }
        };

        if (errors is not null)
            problem.Extensions["errors"] = errors;

        var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
