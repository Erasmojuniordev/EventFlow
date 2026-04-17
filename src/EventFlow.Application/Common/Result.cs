namespace EventFlow.Application.Common;

/// <summary>
/// Pattern Result<T>: representa sucesso ou falha SEM lançar exception.
///
/// POR QUÊ usar Result<T> ao invés de jogar exceptions?
///
/// Problema com exceptions para fluxo de controle:
///   try { var ticket = await bookTicket.Handle(cmd); }
///   catch (NotFoundException e) { return NotFound(e.Message); }
///   catch (ValidationException e) { return BadRequest(e.Message); }
///   catch (DomainException e) { return Conflict(e.Message); }
///   // E se esquecer algum catch? 500 inesperado.
///
/// Com Result<T>:
///   var result = await bookTicket.Handle(cmd);
///   if (!result.IsSuccess) return result.ToProblemDetails(); // centralizado
///   return Ok(result.Value);
///
/// VANTAGENS:
/// - O tipo de retorno documenta explicitamente que a operação pode falhar
/// - Compilador força você a lidar com o erro (sem esquecimento)
/// - Performance: sem stack unwinding de exceptions
/// - Testes: `result.IsSuccess.Should().BeFalse()` é mais claro que `Assert.Throws<X>()`
///
/// ALTERNATIVA: usar a biblioteca `FluentResults` ou `ErrorOr`.
/// Aqui implementamos manualmente para fins didáticos — é simples o suficiente.
///
/// QUANDO usar exception então?
/// - Bugs / estados inesperados (ex: null onde nunca deveria ser null)
/// - Erros de infraestrutura (banco fora do ar) — esses vão para o ExceptionMiddleware
/// - DomainExceptions de invariantes violadas — também capturadas pelo middleware
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public ResultErrorType ErrorType { get; }

    // protected: permite que subclasses passem o value sem expor construtor público
    protected Result(T value)
    {
        IsSuccess = true;
        Value = value;
    }

    protected Result(string error, ResultErrorType errorType)
    {
        IsSuccess = false;
        Error = error;
        ErrorType = errorType;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(string error, ResultErrorType errorType = ResultErrorType.BusinessRule)
        => new(error, errorType);

    public static Result<T> NotFound(string error) => new(error, ResultErrorType.NotFound);

    public static Result<T> Forbidden(string error) => new(error, ResultErrorType.Forbidden);

    public static Result<T> Conflict(string error) => new(error, ResultErrorType.Conflict);
}

/// <summary>
/// Tipo de erro para o controller saber qual status HTTP retornar.
/// </summary>
public enum ResultErrorType
{
    NotFound,       // → 404
    Forbidden,      // → 403
    Conflict,       // → 409
    BusinessRule,   // → 422 Unprocessable Entity
    Validation      // → 400 Bad Request (geralmente vem do FluentValidation pipeline)
}
