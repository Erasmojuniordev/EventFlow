namespace EventFlow.Application.Interfaces;

/// <summary>
/// Repositório para gerenciar Refresh Tokens no banco de dados.
///
/// POR QUÊ salvar refresh token no banco se JWT é stateless?
/// - O refresh token tem que ser stateful para poder ser REVOGADO (logout, suspeita de roubo)
/// - Salvamos o HASH do token, não o valor em texto puro
///
/// POR QUÊ salvar o hash e não o token puro?
/// - Se o banco vazar, tokens em texto puro poderiam ser usados diretamente
/// - Com SHA-256, o atacante teria que inverter o hash (computacionalmente inviável)
/// - Similar à forma como senhas são hashadas — nunca salvar segredos em plaintext
///
/// ROTAÇÃO DE TOKEN:
/// A cada uso do refresh token, geramos um novo par (access + refresh).
/// O token antigo é INVALIDADO. Isso limita o dano se um token vazar:
/// quando o usuário legítimo fizer o próximo refresh, o token roubado falha.
/// Em sistemas mais avançados: isso detecta roubo (Refresh Token Rotation com Reuse Detection).
/// </summary>
public interface IRefreshTokenRepository
{
    Task<string?> GetUserIdByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task SaveAsync(Guid userId, string tokenHash, DateTime expiresAt, CancellationToken cancellationToken = default);

    /// <summary>Revoga um token específico (logout ou rotação).</summary>
    Task RevokeAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Revoga todos os tokens do usuário (logout de todos os dispositivos).</summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
