using EventFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EventFlow.Infrastructure.Interceptors;

/// <summary>
/// Interceptor EF Core que atualiza UpdatedAt automaticamente antes de salvar.
///
/// O QUE É UM INTERCEPTOR EF CORE?
/// É um hook que o EF chama em momentos específicos do ciclo de vida.
/// SaveChangesInterceptor: executado antes/depois de SaveChanges().
///
/// POR QUÊ interceptor ao invés de override do SaveChangesAsync() no DbContext?
/// - Interceptores são reutilizáveis e testáveis separadamente
/// - O DbContext não precisa "saber" de lógica de auditoria (SRP)
/// - Podem ser encadeados: AuditInterceptor + SoftDeleteInterceptor, por exemplo
///
/// ALTERNATIVA DISCUTIDA: usar eventos de EF (ChangeTracker.StateChanged)
/// Funciona também, mas interceptores são o approach mais moderno (.NET 6+)
///
/// POR QUÊ não deixar a entidade setar UpdatedAt ela mesma em cada método?
/// - Esquecimento: um método futuro pode esquecer de chamar SetUpdatedAt()
/// - Com o interceptor, É IMPOSSÍVEL salvar sem atualizar o campo
/// </summary>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateTimestamps(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateTimestamps(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void UpdateTimestamps(DbContext? context)
    {
        if (context is null) return;

        var now = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.SetUpdatedAt(now);
            }
        }
    }
}
