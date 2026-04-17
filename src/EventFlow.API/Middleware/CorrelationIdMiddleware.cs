namespace EventFlow.API.Middleware;

/// <summary>
/// Middleware que garante que cada request tenha um ID de correlação único.
///
/// O QUE É UM CORRELATION ID?
/// É um identificador único por request que é passado em todos os logs daquela request.
/// Com ele, você consegue filtrar no log tudo que aconteceu em uma request específica.
///
/// CENÁRIO SEM CORRELATION ID (log confuso com requests paralelas):
///   [INFO] Iniciando LoginCommand         ← qual request?
///   [INFO] Iniciando BookTicketCommand    ← qual request?
///   [INFO] LoginCommand concluída em 45ms ← qual request?
///
/// CENÁRIO COM CORRELATION ID:
///   [INFO] {CorrelationId: "abc-123"} Iniciando LoginCommand
///   [INFO] {CorrelationId: "xyz-789"} Iniciando BookTicketCommand
///   [INFO] {CorrelationId: "abc-123"} LoginCommand concluída em 45ms
///
/// Agora você consegue filtrar: WHERE CorrelationId = "abc-123" → vê tudo da request de login.
///
/// CONVENÇÃO DE HEADER:
/// "X-Correlation-ID" é o mais comum. Alguns usam "X-Request-ID" ou "TraceParent" (W3C).
/// Se o client já envia o header, reutilizamos o valor (útil em sistemas distribuídos
/// onde o frontend ou API gateway já geram o ID).
///
/// SERILOG: o Correlation ID é adicionado ao LogContext via PushProperty,
/// o que faz com que TODOS os logs naquela request automaticamente incluam o campo.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        // Reutiliza o ID enviado pelo client, ou gera um novo
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N")[..8];  // 8 chars é suficiente para legibilidade

        // Adiciona ao response para o client poder correlacionar também
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Adiciona ao HttpContext.Items para que outros middlewares/handlers acessem
        context.Items["CorrelationId"] = correlationId;

        // Adiciona ao Serilog LogContext — propaga automaticamente para todos os logs da request
        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
