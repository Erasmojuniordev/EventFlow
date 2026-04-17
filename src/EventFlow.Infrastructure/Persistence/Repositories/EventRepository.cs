using EventFlow.Domain.Entities;
using EventFlow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação do IEventRepository usando EF Core + PostgreSQL.
///
/// NOTA sobre AsNoTracking():
/// Queries de leitura (GetAll, GetById para exibição) usam AsNoTracking().
/// POR QUÊ?
/// - EF Core rastreia entidades tracked na memória para detectar mudanças no SaveChanges
/// - Para leitura pura, esse tracking é desperdício de memória e CPU
/// - AsNoTracking() é ~30% mais rápido para reads simples
///
/// QUANDO NÃO usar AsNoTracking()?
/// - Quando você vai MODIFICAR a entidade: se não está tracked, o EF não detecta as mudanças
/// - GetByIdWithTicketsAsync(): usada antes de cancelar ingresso → SEM AsNoTracking
/// - GetByIdAsync() para leitura → COM AsNoTracking
///
/// SOBRE Include() (eager loading):
/// - Include(e => e.Tickets): carrega os tickets JUNTO com o evento em uma query SQL com JOIN
/// - Alternativa: lazy loading (carrega sob demanda) — perigoso: N+1 problem
///   N+1: para 10 eventos, faz 1 query de eventos + 10 queries de tickets = 11 queries
///   Include faz tudo em 1 query — muito mais eficiente
/// </summary>
public sealed class EventRepository(AppDbContext context) : IEventRepository
{
    public async Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<Event?> GetByIdTrackedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // SEM AsNoTracking — retornamos para modificação
        return await context.Events
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<Event?> GetByIdWithTicketsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // SEM AsNoTracking — retornamos para modificação
        return await context.Events
            .Include(e => e.Tickets)
            .Include(e => e.WaitlistEntries)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Event>> GetAllPublishedAsync(CancellationToken cancellationToken = default)
    {
        return await context.Events
            .AsNoTracking()
            .Where(e => e.Status == Domain.Enums.EventStatus.Published)
            .OrderBy(e => e.StartsAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Event>> GetByOrganizerIdAsync(
        Guid organizerId,
        CancellationToken cancellationToken = default)
    {
        return await context.Events
            .AsNoTracking()
            .Where(e => e.OrganizerId == organizerId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Event @event, CancellationToken cancellationToken = default)
    {
        await context.Events.AddAsync(@event, cancellationToken);
        // Sem SaveChanges aqui — responsabilidade do handler/Unit of Work
        // POR QUÊ? Permite que o handler agrupe múltiplas operações em uma transação
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Events.AnyAsync(e => e.Id == id, cancellationToken);
    }
}
