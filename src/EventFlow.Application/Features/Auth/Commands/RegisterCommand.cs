using EventFlow.Application.Common;
using EventFlow.Application.Features.Auth.DTOs;
using EventFlow.Application.Interfaces;
using EventFlow.Domain.Enums;
using MediatR;

namespace EventFlow.Application.Features.Auth.Commands;

/// <summary>
/// Command para registro de novo usuário.
///
/// CQRS: Command = operação que muda estado (write). Não retorna dados além de confirmação.
/// Por convenção, Commands retornam Result<T> onde T é o mínimo necessário.
///
/// Por que o Role vem como string e não enum?
/// - Frontend envia "Attendee" ou "Organizer" — string é mais natural no HTTP
/// - Validamos no RegisterCommandValidator se o role é válido
/// - Admin não pode ser auto-registrado: a validação bloqueia role == "Admin"
/// </summary>
public record RegisterCommand(
    string Name,
    string Email,
    string Password,
    string Role
) : IRequest<Result<AuthResponse>>;

public sealed class RegisterCommandHandler(
    IIdentityService identityService,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepository)
    : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    // Access token dura 15 minutos — configurável, mas hardcoded aqui por simplicidade.
    // Em produção: ler de IOptions<JwtSettings>.
    private const int AccessTokenExpiresInSeconds = 900;
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public async Task<Result<AuthResponse>> Handle(
        RegisterCommand command,
        CancellationToken cancellationToken)
    {
        // Verifica email duplicado antes de tentar criar
        // Por quê não deixar o Identity retornar o erro?
        // — O Identity retorna string[] com mensagens genéricas. Prefiro tratar explicitamente.
        if (await identityService.UserExistsAsync(command.Email))
            return Result<AuthResponse>.Conflict("E-mail já cadastrado.");

        var (success, errors) = await identityService.RegisterAsync(
            command.Name,
            command.Email,
            command.Password,
            command.Role);

        if (!success)
            return Result<AuthResponse>.Failure(string.Join("; ", errors));

        // Após registro bem-sucedido, já faz o login automático (UX melhor)
        var (loginSuccess, userId, roles) = await identityService.LoginAsync(command.Email, command.Password);

        if (!loginSuccess)
            return Result<AuthResponse>.Failure("Registro realizado, mas houve erro ao fazer login automático.");

        var accessToken = tokenService.GenerateAccessToken(userId, command.Email, roles);
        var (refreshPlaintext, refreshHash) = tokenService.GenerateRefreshToken();

        await refreshTokenRepository.SaveAsync(
            userId,
            refreshHash,
            DateTime.UtcNow.Add(RefreshTokenLifetime),
            cancellationToken);

        var response = new AuthResponse(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresInSeconds: AccessTokenExpiresInSeconds,
            User: new UserInfo(userId, command.Name, command.Email, roles)
        );

        // ATENÇÃO: o RefreshToken plaintext precisa ir para o controller setar o cookie.
        // Mas Result<AuthResponse> não carrega isso.
        //
        // SOLUÇÃO ADOTADA: retornamos o refreshToken como parte interna via uma subclasse,
        // e o controller extrai. Alternativa seria um envelope maior, mas isso funciona.
        //
        // NOTA: em alguns projetos, o handler recebe IHttpContextAccessor e seta o cookie ele mesmo.
        // Optamos por NÃO fazer isso — manter infraestrutura HTTP fora do Application layer.
        // O controller é o lugar correto para mexer em cookies.
        return new AuthResultWithRefreshToken(response, refreshPlaintext);
    }
}

/// <summary>
/// Subclasse que carrega o refresh token plaintext para o controller setar o cookie.
/// O controller faz cast para esta classe após receber o Result do handler.
/// </summary>
public sealed class AuthResultWithRefreshToken(AuthResponse response, string refreshToken)
    : Result<AuthResponse>(response)
{
    public string RefreshToken { get; } = refreshToken;
}
