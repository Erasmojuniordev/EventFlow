namespace EventFlow.Domain.Enums;

/// <summary>
/// Status de ciclo de vida de um evento.
///
/// Isso é uma máquina de estados simples. As transições válidas são:
///   Draft → Published
///   Draft → Cancelled
///   Published → Cancelled
///   Published → Completed  (automático quando a data do evento passa)
///
/// Transições INVÁLIDAS (geram DomainException):
///   Cancelled → qualquer coisa  (cancelado é estado terminal)
///   Completed → qualquer coisa  (completado é estado terminal)
///   Published → Draft           (não dá para "despublicar" — attendees já podem ter ingressos)
///
/// Por que modelar isso como enum + validação no domain ao invés de bool flags (IsPublished, IsCancelled)?
/// - Estados mutualmente exclusivos ficam claros: um evento não pode ser Published E Cancelled ao mesmo tempo
/// - Fácil de adicionar novos estados no futuro sem criar combinações inválidas de booleans
/// - Queries ficam mais legíveis: WHERE status = 'Published' vs WHERE is_published = true AND is_cancelled = false
/// </summary>
public enum EventStatus
{
    /// <summary>Criado pelo organizador, não visível para attendees ainda.</summary>
    Draft = 0,

    /// <summary>Visível publicamente, ingressos podem ser reservados.</summary>
    Published = 1,

    /// <summary>Cancelado. Todos os ingressos confirmados devem ser cancelados. Estado terminal.</summary>
    Cancelled = 2,

    /// <summary>Evento encerrado. Ingressos não podem mais ser alterados. Estado terminal.</summary>
    Completed = 3
}
