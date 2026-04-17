namespace EventFlow.Application.Interfaces;

/// <summary>
/// Contrato para geração e validação de JWT e Refresh Tokens.
///
/// Por que essa interface fica no Application e não no Domain?
/// - JWT é um detalhe de infraestrutura (depende de libs externas)
/// - Application orquestra o fluxo: "se login OK, peça um token ao ITokenService"
/// - A implementação concreta (TokenService) vive na Infrastructure
///
/// Por que separar AccessToken e RefreshToken?
/// ACCESS TOKEN (JWT):
///   - Curta duração (15 min) — se vazar, o dano é limitado temporalmente
///   - Stateless: validado pela assinatura, sem consulta ao banco
///   - Enviado no header Authorization: Bearer {token}
///
/// REFRESH TOKEN:
///   - Longa duração (7 dias) — permite renovar access tokens sem re-login
///   - Stateful: salvo no banco (hasheado) para poder ser revogado
///   - Enviado em cookie httpOnly — JavaScript NÃO consegue ler (proteção contra XSS)
///
/// FLUXO:
///   Login → API retorna access token (JSON) + refresh token (cookie httpOnly)
///   Access token expira → client usa o cookie para chamar /auth/refresh
///   API valida refresh token no banco → emite novo par (rotação de token)
///   Logout → server apaga o refresh token do banco → cookie não serve mais
///
/// POR QUÊ cookie httpOnly para refresh token e não localStorage?
///   localStorage é acessível por JavaScript → vulnerável a XSS
///   cookie httpOnly não é acessível por JS → XSS não consegue roubá-lo
///   Mas cookie é vulnerável a CSRF → mitigamos com SameSite=Strict e CSRF token se necessário
/// </summary>
public interface ITokenService
{
    /// <summary>Gera um JWT de curta duração para o usuário.</summary>
    string GenerateAccessToken(Guid userId, string email, IEnumerable<string> roles);

    /// <summary>
    /// Gera um refresh token aleatório e retorna o valor em plaintext (para enviar ao client)
    /// e o hash SHA-256 (para salvar no banco — nunca salvar plaintext).
    /// </summary>
    (string plaintext, string hash) GenerateRefreshToken();

    /// <summary>Extrai o userId de um access token, mesmo que expirado (para refresh).</summary>
    Guid? GetUserIdFromExpiredToken(string accessToken);
}
