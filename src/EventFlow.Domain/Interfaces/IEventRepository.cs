using EventFlow.Domain.Entities;
using EventFlow.Domain.Enums;

namespace EventFlow.Domain.Interfaces;

/// <summary>
/// Contrato do repositório de eventos.
///
/// POR QUÊ a interface fica no Domain e a implementação na Infrastructure?
/// Isso é o Princípio da Inversão de Dependência (DIP — o D do SOLID).
///
/// O fluxo de dependência correto é:
///   API → Application → Domain ← Infrastructure
///                                      ↑
///                              implementa a interface
///
/// Se o Domain dependesse da Infrastructure (EF Core, Npgsql), não poderíamos
/// testar o Domain sem banco de dados. Com interfaces, nos testes usamos Mocks.
///
/// POR QUÊ não usar IRepository<T> genérico?
/// - Repositório genérico tende a vazar abstrações: nem toda entidade precisa de Delete, por exemplo
/// - Interfaces específicas deixam claras quais operações são suportadas por entidade
/// - Mais verboso, mas mais expressivo e fácil de mockar nos testes
/// </summary>
public interface IEventRepository
{
    /// <summary>Busca para leitura (AsNoTracking) — mais rápido, não pode ser modificado.</summary>
    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca tracked para modificação (Update, Publish, Cancel).
    /// Sem AsNoTracking — o EF detecta as mudanças e persiste no SaveChanges.
    /// </summary>
    Task<Event?> GetByIdTrackedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Busca evento incluindo Tickets (para verificar capacidade ou cancelar em cascata).</summary>
    Task<Event?> GetByIdWithTicketsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Event>> GetAllPublishedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Event>> GetByOrganizerIdAsync(Guid organizerId, CancellationToken cancellationToken = default);

    Task AddAsync(Event @event, CancellationToken cancellationToken = default);

    // Por que não ter Update() separado?
    // Com EF Core, simplesmente alterar a entidade tracked e chamar SaveChanges é suficiente.
    // Um Update() explícito seria redundante e confuso.
    // O repositório gerencia o contexto — não precisa de método Update.

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
