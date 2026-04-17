namespace EventFlow.Infrastructure.Identity;

/// <summary>
/// Entidade de banco para armazenar refresh tokens hasheados.
///
/// Campos:
/// - TokenHash: SHA-256 do token plaintext. NUNCA salvar o token original.
/// - UserId: para saber a quem o token pertence (para logout geral)
/// - ExpiresAt: para limpeza de tokens expirados (job periódico recomendado)
/// - RevokedAt: null = válido, preenchido = inválido (soft delete)
///   POR QUÊ soft delete ao invés de deletar fisicamente?
///   Permite auditoria: "esse token foi usado depois de revogado?" (detecta ataques)
///
/// LIMPEZA: tokens expirados/revogados acumulam no banco.
/// Em produção: job periódico que deleta registros onde ExpiresAt < now - 30d
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
