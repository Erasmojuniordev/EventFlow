using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EventFlow.Application.Behaviors;

/// <summary>
/// Pipeline Behavior que loga automaticamente TODA request/response do MediatR.
///
/// POR QUÊ logar no pipeline ao invés de em cada handler?
/// - Um único lugar garante cobertura total — nenhum handler fica sem log
/// - Handlers ficam limpos: só lógica de negócio
/// - Facilita debugging: toda request tem entry/exit no log
///
/// O QUE LOGAR (e o que NÃO logar):
/// LOGAR:
///   - Nome da request (comando/query)
///   - Tempo de execução
///   - Se deu erro (com exceção)
///
/// NÃO LOGAR:
///   - Conteúdo do request completo (pode ter dados sensíveis: senha, CPF, cartão)
///   - Response completo (pode ter dados pessoais)
///
/// STRUCTURED LOGGING com Serilog:
/// _logger.LogInformation("Handling {RequestName} in {ElapsedMs}ms", ...)
/// O {RequestName} vira uma propriedade indexada — permite filtrar logs por comando:
///   WHERE RequestName = 'BookTicketCommand'
/// Isso é muito mais poderoso que concatenação de strings no log.
///
/// NÍVEL DE LOG:
/// - Information: requests normais
/// - Warning: requests lentas (> 500ms) — sinal de problema de performance
/// - Error: exceptions — também capturadas pelo ExceptionMiddleware, mas bom ter aqui também
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowRequestThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Iniciando {RequestName}", requestName);

            var response = await next();

            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs)
            {
                // Warning para requests lentas — útil para monitoramento de performance
                logger.LogWarning(
                    "Request lenta detectada: {RequestName} levou {ElapsedMs}ms (threshold: {ThresholdMs}ms)",
                    requestName,
                    stopwatch.ElapsedMilliseconds,
                    SlowRequestThresholdMs);
            }
            else
            {
                logger.LogInformation(
                    "{RequestName} concluída em {ElapsedMs}ms",
                    requestName,
                    stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(
                ex,
                "Erro ao processar {RequestName} após {ElapsedMs}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);
            throw;  // relança — o ExceptionMiddleware trata e formata a resposta
        }
    }
}
