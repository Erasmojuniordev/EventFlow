using EventFlow.Domain.Enums;
using EventFlow.Domain.Exceptions;

namespace EventFlow.Domain.Entities;

/// <summary>
/// Entidade principal: um evento gerenciado por um Organizer.
///
/// REGRAS DE NEGÓCIO embutidas aqui (não no service, não no controller):
/// - Só pode publicar se tiver data futura e capacidade > 0
/// - Só pode cancelar se não estiver Completed
/// - Não pode editar evento Published (exceto campos específicos)
/// - Completar evento é só possível se estiver Published
///
/// POR QUÊ colocar as regras na entidade e não no handler (Application)?
/// - A entidade fica "autoprotetora": impossível criar um estado inválido de fora
/// - Qualquer handler que chame Publish() automaticamente respeita as regras
/// - Se colocarmos no handler, outro handler futuro pode "esquecer" de validar
/// - Isso é o princípio "tell, don't ask": diga ao evento para publicar, não pergunte se pode e depois publique
///
/// ALTERNATIVA DISCUTIDA: Domain Services
/// Quando a regra envolve MAIS de uma entidade, aí sim vai para um Domain Service.
/// Ex: "Não pode ter dois eventos do mesmo organizer no mesmo horário" — isso precisaria
/// consultar outros eventos, então não pertence à entidade Event isolada.
/// </summary>
public class Event : BaseEntity
{
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public int Capacity { get; private set; }
    public EventStatus Status { get; private set; }
    public Guid OrganizerId { get; private set; }

    // EF Core precisa de coleção privada para evitar que código externo
    // adicione tickets direto na coleção, burlando as regras de negócio.
    // Expõe como IReadOnlyCollection para leitura.
    private readonly List<Ticket> _tickets = [];
    public IReadOnlyCollection<Ticket> Tickets => _tickets.AsReadOnly();

    private readonly List<WaitlistEntry> _waitlistEntries = [];
    public IReadOnlyCollection<WaitlistEntry> WaitlistEntries => _waitlistEntries.AsReadOnly();

    // Propriedade calculada — número de ingressos que ainda podem ser reservados
    // NotMapped no EF (configurado via Fluent API na Infrastructure)
    public int AvailableSpots => Capacity - _tickets.Count(t =>
        t.Status == TicketStatus.Reserved || t.Status == TicketStatus.Confirmed);

    public bool IsFull => AvailableSpots <= 0;

    // Construtor privado para EF Core (ele usa reflection para criar instâncias)
    private Event() { }

    // Factory method — preferível a construtor público com muitos parâmetros.
    // Vantagem: nome descritivo, pode ter validação antes de criar.
    public static Event Create(
        string title,
        string description,
        string location,
        DateTime startsAt,
        DateTime endsAt,
        int capacity,
        Guid organizerId)
    {
        // Validações de invariantes do domínio (não de input do usuário — isso é no FluentValidation)
        if (endsAt <= startsAt)
            throw new DomainException("A data de término deve ser após a data de início.");

        if (capacity <= 0)
            throw new DomainException("A capacidade deve ser maior que zero.");

        return new Event
        {
            Title = title,
            Description = description,
            Location = location,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Capacity = capacity,
            OrganizerId = organizerId,
            Status = EventStatus.Draft  // sempre começa como rascunho
        };
    }

    /// <summary>
    /// Publica o evento, tornando-o visível e disponível para reservas.
    /// </summary>
    public void Publish()
    {
        if (Status != EventStatus.Draft)
            throw new DomainException($"Não é possível publicar um evento com status '{Status}'.");

        if (StartsAt <= DateTime.UtcNow)
            throw new DomainException("Não é possível publicar um evento com data no passado.");

        Status = EventStatus.Published;
    }

    /// <summary>
    /// Cancela o evento. Quem cancelar deve garantir que os ingressos também sejam cancelados
    /// (responsabilidade do CancelEventHandler no Application layer).
    /// </summary>
    public void Cancel()
    {
        if (Status == EventStatus.Completed)
            throw new DomainException("Não é possível cancelar um evento já concluído.");

        if (Status == EventStatus.Cancelled)
            throw new DomainException("O evento já está cancelado.");

        Status = EventStatus.Cancelled;
    }

    /// <summary>
    /// Marca o evento como concluído. Normalmente chamado por um job automático
    /// quando EndsAt passa, mas pode ser chamado manualmente pelo Organizer.
    /// </summary>
    public void Complete()
    {
        if (Status != EventStatus.Published)
            throw new DomainException($"Só é possível concluir um evento Published. Status atual: '{Status}'.");

        Status = EventStatus.Completed;
    }

    /// <summary>
    /// Marca a linha do evento como modificada para acionar a verificação de concorrência otimista (xmin).
    ///
    /// POR QUÊ isso é necessário ao reservar um ingresso?
    /// - O Ticket é inserido em uma linha separada (tabela tickets)
    /// - Inserir um Ticket NÃO altera a linha do Event — o xmin do Event não mudaria
    /// - Sem tocar o Event, duas reservas simultâneas podem ambas passar na verificação de capacidade
    /// - Ao chamar Touch(), o handler força um UPDATE na linha do Event
    /// - EF Core adiciona: WHERE xmin = {valor lido no início da request}
    /// - Se outro request modificou o Event nesse meio tempo, o WHERE não bate → DbUpdateConcurrencyException
    ///
    /// FLUXO COMPLETO:
    ///   Request A: lê Event (xmin=42), capacidade livre → cria Ticket + Touch() → SaveChanges
    ///   Request B: lê Event (xmin=42), capacidade livre → cria Ticket + Touch() → SaveChanges
    ///   Request A persiste: UPDATE events SET updated_at=... WHERE id=... AND xmin=42 → OK, xmin vira 43
    ///   Request B falha:   UPDATE events SET updated_at=... WHERE id=... AND xmin=42 → 0 linhas → EXCEÇÃO
    ///   Request B (retry): recarrega Event (xmin=43), capacidade cheia → vai para waitlist
    /// </summary>
    public void Touch()
    {
        SetUpdatedAt(DateTime.UtcNow);
    }

    /// <summary>
    /// Atualiza campos editáveis. Só permitido em Draft.
    /// Em Published, apenas o cancelamento é permitido.
    /// </summary>
    public void Update(
        string title,
        string description,
        string location,
        DateTime startsAt,
        DateTime endsAt,
        int capacity)
    {
        if (Status != EventStatus.Draft)
            throw new DomainException("Só é possível editar eventos em rascunho (Draft).");

        if (endsAt <= startsAt)
            throw new DomainException("A data de término deve ser após a data de início.");

        // Por quê validar capacidade vs ingressos existentes?
        // Se o evento tivesse ingressos (não possível em Draft, mas por segurança):
        int activeTickets = _tickets.Count(t =>
            t.Status == TicketStatus.Reserved || t.Status == TicketStatus.Confirmed);

        if (capacity < activeTickets)
            throw new DomainException(
                $"A nova capacidade ({capacity}) não pode ser menor que os ingressos ativos ({activeTickets}).");

        Title = title;
        Description = description;
        Location = location;
        StartsAt = startsAt;
        EndsAt = endsAt;
        Capacity = capacity;
    }
}
