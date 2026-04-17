using EventFlow.Application.Common;
using EventFlow.Application.Features.Auth.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EventFlow.API.Controllers;

/// <summary>
/// Controller de autenticação.
///
/// CONTROLLERS THIN (magros):
/// O controller faz APENAS:
/// 1. Receber a request e extrair dados
/// 2. Enviar para o MediatR (que encontra o handler correto)
/// 3. Converter o Result em resposta HTTP
///
/// O controller NÃO faz: validação, lógica de negócio, acesso ao banco.
/// Tudo isso está no handler (Application layer).
///
/// POR QUÊ [ApiController]?
/// - Ativa model binding automático (lê JSON do body sem [FromBody] explícito)
/// - Retorna 400 automático para model inválido (mas como usamos FluentValidation no pipeline, isso é redundante)
/// - Ativa [Route] obrigatório
///
/// COOKIE httpOnly para refresh token:
/// O controller é o lugar correto para mexer em cookies — é infraestrutura HTTP.
/// O Application layer retorna o refresh token plaintext via AuthResultWithRefreshToken,
/// e o controller seta o cookie. Separação clara de responsabilidades.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    private const string RefreshTokenCookieName = "refresh_token";

    /// <summary>Registra um novo usuário (Attendee ou Organizer).</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterCommand(request.Name, request.Email, request.Password, request.Role);
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Created(string.Empty, MapToResponse(result))
            : MapErrorToResult(result);
    }

    /// <summary>Autentica um usuário e retorna access token + seta cookie de refresh.</summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]  // definido no Program.cs
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return MapErrorToResult(result);

        SetRefreshTokenCookie(result);
        return Ok(MapToResponse(result));
    }

    /// <summary>Renova o access token usando o refresh token do cookie.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        // Lê o refresh token do cookie httpOnly — não vem no body
        var refreshToken = Request.Cookies[RefreshTokenCookieName];

        if (string.IsNullOrEmpty(refreshToken))
            return Forbid();

        var command = new RefreshTokenCommand(request.AccessToken, refreshToken);
        var result = await sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return MapErrorToResult(result);

        SetRefreshTokenCookie(result);
        return Ok(MapToResponse(result));
    }

    /// <summary>Invalida o refresh token (logout).</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName] ?? string.Empty;

        var command = new LogoutCommand(refreshToken);
        await sender.Send(command, cancellationToken);

        // Apaga o cookie no client
        Response.Cookies.Delete(RefreshTokenCookieName);

        return NoContent();
    }

    // --- Métodos privados auxiliares ---

    private void SetRefreshTokenCookie(Result<Application.Features.Auth.DTOs.AuthResponse> result)
    {
        if (result is not AuthResultWithRefreshToken tokenResult) return;

        Response.Cookies.Append(RefreshTokenCookieName, tokenResult.RefreshToken, new CookieOptions
        {
            HttpOnly = true,  // JavaScript não pode ler — proteção contra XSS
            Secure = true,    // Só enviado via HTTPS (desabilitar em dev se necessário)
            SameSite = SameSiteMode.Strict,  // Não enviado em requests cross-site — proteção contra CSRF
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }

    private static AuthResponseDto MapToResponse(Result<Application.Features.Auth.DTOs.AuthResponse> result)
    {
        var v = result.Value!;
        return new AuthResponseDto(v.AccessToken, v.TokenType, v.ExpiresInSeconds,
            new UserDto(v.User.Id, v.User.Name, v.User.Email, v.User.Roles));
    }

    private IActionResult MapErrorToResult<T>(Result<T> result)
    {
        return result.ErrorType switch
        {
            ResultErrorType.NotFound => NotFound(new ProblemDetails { Title = "Não encontrado", Detail = result.Error, Status = 404 }),
            ResultErrorType.Forbidden => StatusCode(403, new ProblemDetails { Title = "Acesso negado", Detail = result.Error, Status = 403 }),
            ResultErrorType.Conflict => Conflict(new ProblemDetails { Title = "Conflito", Detail = result.Error, Status = 409 }),
            _ => UnprocessableEntity(new ProblemDetails { Title = "Erro de negócio", Detail = result.Error, Status = 422 })
        };
    }
}

// --- Request/Response DTOs do controller (contratos HTTP) ---
// Separados dos DTOs do Application layer propositalmente:
// Controller DTOs = formato da API (o que o client envia/recebe)
// Application DTOs = formato interno da aplicação
// Nem sempre são iguais — versionamento de API fica mais fácil assim

public record RegisterRequest(string Name, string Email, string Password, string Role);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string AccessToken);

public record AuthResponseDto(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    UserDto User);

public record UserDto(Guid Id, string Name, string Email, IList<string> Roles);
