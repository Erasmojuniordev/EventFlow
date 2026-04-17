using EventFlow.Application.Common;
using EventFlow.Application.Features.Auth.DTOs;
using EventFlow.Application.Interfaces;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace EventFlow.Application.Features.Auth.Commands;

/// <summary>
/// Command para renovar o par access token + refresh token.
///
/// FLUXO DO REFRESH:
/// 1. Client percebe que o access token expirou (status 401)
/// 2. Client faz POST /auth/refresh — o cookie httpOnly é enviado automaticamente pelo browser
/// 3. API lê o refresh token do cookie, valida no banco, gera novo par
/// 4. Token antigo é INVALIDADO (rotação) — novo par é retornado
///
/// O expired access token é aceito aqui propositalmente:
/// precisamos dele para extrair o userId e verificar quem está fazendo o refresh.
/// </summary>
public record RefreshTokenCommand(
    string ExpiredAccessToken,
    string RefreshTokenPlaintext
) : IRequest<Result<AuthResponse>>;

public sealed class RefreshTokenCommandHandler(
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepository,
    IIdentityService identityService)
    : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private const int AccessTokenExpiresInSeconds = 900;
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public async Task<Result<AuthResponse>> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        // Extrai userId do access token EXPIRADO (sem validar a expiração)
        var userId = tokenService.GetUserIdFromExpiredToken(command.ExpiredAccessToken);
        if (userId is null)
            return Result<AuthResponse>.Forbidden("Token inválido.");

        // Gera o hash do plaintext para buscar no banco
        var tokenHash = ComputeSha256Hash(command.RefreshTokenPlaintext);

        var storedUserId = await refreshTokenRepository.GetUserIdByTokenHashAsync(
            tokenHash, cancellationToken);

        if (storedUserId is null || storedUserId != userId.Value.ToString())
            return Result<AuthResponse>.Forbidden("Refresh token inválido ou expirado.");

        // ROTAÇÃO: invalida o token antigo antes de gerar o novo
        // Por quê antes? Se gerarmos o novo primeiro e a invalidação falhar,
        // o token antigo continua válido — brecha de segurança.
        await refreshTokenRepository.RevokeAsync(tokenHash, cancellationToken);

        // Gera novo par
        var (_, roles) = await GetUserRolesAsync(userId.Value);
        var newAccessToken = tokenService.GenerateAccessToken(userId.Value, command.ExpiredAccessToken, roles);
        var (newRefreshPlaintext, newRefreshHash) = tokenService.GenerateRefreshToken();

        await refreshTokenRepository.SaveAsync(
            userId.Value,
            newRefreshHash,
            DateTime.UtcNow.Add(RefreshTokenLifetime),
            cancellationToken);

        var response = new AuthResponse(
            AccessToken: newAccessToken,
            TokenType: "Bearer",
            ExpiresInSeconds: AccessTokenExpiresInSeconds,
            User: new UserInfo(userId.Value, string.Empty, string.Empty, roles.ToList())
        );

        return new AuthResultWithRefreshToken(response, newRefreshPlaintext);
    }

    private async Task<(string email, IEnumerable<string> roles)> GetUserRolesAsync(Guid userId)
    {
        // Simplificação: roles virão do token expirado nesta versão.
        // Em produção, buscar do banco garante que revogações de role sejam respeitadas.
        return (string.Empty, Array.Empty<string>());
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
