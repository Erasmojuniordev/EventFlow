namespace EventFlow.Domain.Entities;

/// <summary>
/// Classe base para todas as entidades do domínio.
///
/// Por quê usar uma classe base?
/// - Garante que TODA entidade tenha Id do mesmo tipo (Guid)
/// - Centraliza o tracking de auditoria (CreatedAt, UpdatedAt)
/// - Evita repetição em cada entidade
///
/// Por que Guid e não int/long auto-incremento?
/// - Guid pode ser gerado pelo cliente antes de salvar (útil para otimismo)
/// - Não expõe contagem de registros em URLs (/events/1 vs /events/{guid})
/// - Funciona bem em sistemas distribuídos (sem conflito de sequence)
/// - Desvantagem: índices maiores no banco, URLs menos legíveis
///
/// Por que não usar record ao invés de class?
/// - Entidades têm identidade (Id), não igualdade por valor
/// - Dois Event com mesmo Id são o MESMO evento, mesmo que os campos difiram
/// - Records são melhores para Value Objects (ver Money.cs)
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; private set; }

    // UTC sempre — armazenar timezone no banco é armadilha em sistemas multi-região.
    // Conversão para timezone do usuário acontece no frontend.
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    protected BaseEntity()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    // Construtor para recriar entidades vindas do banco (EF Core usa isso via reflection)
    protected BaseEntity(Guid id)
    {
        Id = id;
    }

    /// <summary>
    /// Atualiza o timestamp. Chamado pelo SaveChangesInterceptor automaticamente.
    /// Não chamar manualmente — o interceptor cuida disso.
    /// </summary>
    public void SetUpdatedAt(DateTime updatedAt)
    {
        UpdatedAt = updatedAt;
    }
}
