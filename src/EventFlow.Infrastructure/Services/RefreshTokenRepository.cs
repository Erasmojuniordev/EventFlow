using EventFlow.Application.Interfaces;
using EventFlow.Infrastructure.Identity;
using EventFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Infrastructure.Services;

public sealed class RefreshTokenRepository(AppDbContext context) : IRefreshTokenRepository
{
    public async Task<string?> GetUserIdByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        var token = await context.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.TokenHash == tokenHash && r.RevokedAt == null && r.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        return token?.UserId.ToString();
    }

    public async Task SaveAsync(
        Guid userId,
        string tokenHash,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt
        };

        await context.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var token = await context.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);

        if (token is null) return;  // idempotente — sem erro se já não existe

        token.RevokedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // ExecuteUpdate: atualiza diretamente no banco sem carregar os registros em memória
        // POR QUÊ não fazer ToList() + foreach?
        // - ExecuteUpdate gera um UPDATE WHERE — uma query SQL
        // - ToList() + foreach: N queries SQL (uma por token) = N+1 problem
        await context.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(r => r.RevokedAt, DateTime.UtcNow),
                cancellationToken);
    }
}
