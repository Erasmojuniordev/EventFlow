using EventFlow.Application.Common;
using EventFlow.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Infrastructure.Persistence;

/// <summary>
/// Implementação do IUnitOfWork usando o DbContext do EF Core.
/// O DbContext já implementa o padrão internamente — esta classe é só a ponte.
///
/// CONVERSÃO DE EXCEÇÕES:
/// DbUpdateConcurrencyException (EF Core / Infrastructure) é convertida para
/// ConcurrencyException (Application/Common) antes de propagar.
///
/// POR QUÊ converter aqui e não no handler?
/// - O handler está na camada Application e não deve conhecer EF Core
/// - O UnitOfWork está na Infrastructure — é o lugar certo para "traduzir" erros de EF
/// - Mantém a Application isolada de detalhes de infraestrutura
/// </summary>
public sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // 1. Recarrega as entidades conflitantes com os valores atuais do banco.
            //    Isso atualiza o xmin (e demais colunas) para o retry ter o estado correto.
            foreach (var entry in ex.Entries)
                await entry.ReloadAsync(cancellationToken);

            // 2. Remove do ChangeTracker as entidades que foram "Added" mas NÃO foram salvas
            //    (a transação falhou, então elas não estão no banco).
            //    Sem isso, no retry o handler chamaria AddAsync() novamente, resultando em
            //    DUAS entidades "Added" no contexto para o mesmo SaveChanges.
            //
            //    EXEMPLO:
            //    Attempt 1: AddAsync(ticket_A) → SaveChanges falha → ticket_A ainda está no context
            //    Attempt 2: AddAsync(ticket_B) → SaveChanges → insere ticket_A E ticket_B!
            //
            //    Ao fazer Detach aqui, o retry começa com um contexto "limpo" para entidades novas.
            var addedEntries = context.ChangeTracker.Entries()
                .Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added)
                .ToList();

            foreach (var entry in addedEntries)
                entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;

            throw new ConcurrencyException(
                "Conflito de concorrência detectado. A operação será retentada automaticamente.",
                ex);
        }
    }
}
