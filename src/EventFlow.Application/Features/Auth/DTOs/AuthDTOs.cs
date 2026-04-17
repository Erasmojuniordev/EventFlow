namespace EventFlow.Application.Features.Auth.DTOs;

/// <summary>
/// DTOs de resposta para operações de autenticação.
///
/// POR QUÊ usar DTOs ao invés de retornar a entidade diretamente?
/// - Controle sobre o que é exposto na API (nunca expor PasswordHash, por exemplo)
/// - Entidade pode mudar sem quebrar o contrato da API
/// - Facilita versionamento da API no futuro
///
/// POR QUÊ não retornar o RefreshToken aqui?
/// O refresh token é enviado como cookie httpOnly — nunca no body JSON.
/// Isso evita que JavaScript no frontend consiga lê-lo (proteção contra XSS).
/// O controller é responsável por setar o cookie após receber os dados do handler.
/// </summary>
public record AuthResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    UserInfo User
);

public record UserInfo(
    Guid Id,
    string Name,
    string Email,
    IList<string> Roles
);
