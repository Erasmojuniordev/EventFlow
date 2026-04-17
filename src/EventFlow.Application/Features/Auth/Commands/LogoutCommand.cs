using EventFlow.Application.Common;
using EventFlow.Application.Interfaces;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace EventFlow.Application.Features.Auth.Commands;

public record LogoutCommand(string RefreshTokenPlaintext) : IRequest<Result<bool>>;

public sealed class LogoutCommandHandler(IRefreshTokenRepository refreshTokenRepository)
    : IRequestHandler<LogoutCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        var tokenHash = ComputeSha256Hash(command.RefreshTokenPlaintext);
        await refreshTokenRepository.RevokeAsync(tokenHash, cancellationToken);

        // Retorna sucesso mesmo se o token não existir — idempotente.
        // Por quê? Evita que um atacante descubra se o token era válido
        // tentando fazer logout e analisando a resposta.
        return Result<bool>.Success(true);
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
