using EventFlow.Application.Common;
using EventFlow.Application.Features.Auth.DTOs;
using EventFlow.Application.Interfaces;
using MediatR;

namespace EventFlow.Application.Features.Auth.Commands;

/// <summary>
/// Command para login de usuário existente.
/// Retorna AuthResultWithRefreshToken para que o controller possa setar o cookie httpOnly.
/// </summary>
public record LoginCommand(string Email, string Password) : IRequest<Result<AuthResponse>>;

public sealed class LoginCommandHandler(
    IIdentityService identityService,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepository)
    : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private const int AccessTokenExpiresInSeconds = 900; // 15 min
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public async Task<Result<AuthResponse>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var (success, userId, roles) = await identityService.LoginAsync(command.Email, command.Password);

        if (!success)
        {
            // SEGURANÇA: não dizer se o email existe ou a senha está errada.
            // "Credenciais inválidas" é deliberadamente genérico para evitar user enumeration.
            // User enumeration: atacante descobre quais emails estão cadastrados
            // tentando login e analisando a mensagem de erro.
            return Result<AuthResponse>.Failure(
                "Credenciais inválidas.",
                ResultErrorType.Forbidden);
        }

        var accessToken = tokenService.GenerateAccessToken(userId, command.Email, roles);
        var (refreshPlaintext, refreshHash) = tokenService.GenerateRefreshToken();

        await refreshTokenRepository.SaveAsync(
            userId,
            refreshHash,
            DateTime.UtcNow.Add(RefreshTokenLifetime),
            cancellationToken);

        // Buscamos o name do usuário — Identity não retorna no LoginAsync por design
        // (para evitar consulta extra na maioria dos casos).
        // Por simplicidade, o name virá via claim no token. Ajustamos conforme necessário.
        var response = new AuthResponse(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresInSeconds: AccessTokenExpiresInSeconds,
            User: new UserInfo(userId, command.Email, command.Email, roles)
        );

        return new AuthResultWithRefreshToken(response, refreshPlaintext);
    }
}
