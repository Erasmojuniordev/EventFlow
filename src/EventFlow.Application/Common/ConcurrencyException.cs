namespace EventFlow.Application.Common;

/// <summary>
/// Exceção lançada pela camada de Application quando ocorre conflito de concorrência otimista.
///
/// POR QUÊ criar esta exceção em vez de usar DbUpdateConcurrencyException do EF Core diretamente?
///
/// A camada Application não deve depender de infraestrutura (EF Core, Npgsql etc.).
/// Se o Application referenciasse DbUpdateConcurrencyException:
///   - Teríamos o pacote Microsoft.EntityFrameworkCore na Application (viola Clean Architecture)
///   - Trocar o ORM exigiria mudar o Application também
///
/// Com ConcurrencyException no Application:
///   - O IUnitOfWork (Application) a lança — abstrai o detalhe de EF
///   - O UnitOfWork (Infrastructure) captura DbUpdateConcurrencyException e relança como ConcurrencyException
///   - O BookTicketHandler usa ConcurrencyException no Polly — sem conhecer EF
///
/// COMPARAÇÃO:
///   BookTicketHandler (Application) usa:  ConcurrencyException  ← definida aqui
///   UnitOfWork (Infrastructure) usa:      DbUpdateConcurrencyException → ConcurrencyException
///   Polly (Application) retenta em:       ConcurrencyException
/// </summary>
public class ConcurrencyException(string message, Exception? innerException = null)
    : Exception(message, innerException);
