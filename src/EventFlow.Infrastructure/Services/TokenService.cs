using EventFlow.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EventFlow.Infrastructure.Services;

/// <summary>
/// Implementação do ITokenService usando JWT (JSON Web Tokens).
///
/// O QUE É UM JWT?
/// É um token autocontido composto por 3 partes separadas por ponto:
///   Header.Payload.Signature
///
/// Header:  {"alg": "HS256", "typ": "JWT"}  → como foi assinado
/// Payload: {"sub": "userId", "email": "...", "exp": 1234567890, "roles": ["Attendee"]}
/// Signature: HMAC-SHA256(base64(header) + "." + base64(payload), secret)
///
/// VALIDAÇÃO:
/// Qualquer servidor com a secret pode validar o token SEM consultar o banco.
/// Isso é "stateless" — escalável horizontalmente (múltiplas instâncias da API).
///
/// SEGURANÇA — O QUE PROTEGE O JWT:
/// 1. Assinatura: se alguém alterar o payload, a assinatura não bate → rejeitado
/// 2. Expiração (exp): token expirado é rejeitado automaticamente pelo middleware JWT
/// 3. Secret forte: sem a secret, não dá para forjar um token válido
///
/// VULNERABILIDADE: se a secret vazar, qualquer um pode criar tokens válidos.
/// Por isso: secret mínimo de 256 bits, rotação periódica em produção.
///
/// POR QUÊ HS256 e não RS256?
/// - HS256: chave simétrica (mesma chave assina e valida) — simples para uma só API
/// - RS256: chave assimétrica (privada assina, pública valida) — necessário quando
///   múltiplos serviços diferentes precisam validar o token (microservices)
/// - Para este projeto monolítico: HS256 é suficiente
/// </summary>
public sealed class TokenService(IConfiguration configuration) : ITokenService
{
    public string GenerateAccessToken(Guid userId, string email, IEnumerable<string> roles)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secret = jwtSettings["Secret"]
            ?? throw new InvalidOperationException("JWT Secret não configurado. Use variáveis de ambiente.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Claims: informações embutidas no token
        // "sub" (subject) = identificador do usuário — padrão JWT RFC 7519
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            // Jti = ID único do token — útil para invalidação individual (token blacklist)
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        // Adiciona cada role como claim separado — o Identity/ASP.NET Core espera isso
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var expirationMinutes = int.TryParse(jwtSettings["ExpirationMinutes"], out var min) ? min : 15;

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string plaintext, string hash) GenerateRefreshToken()
    {
        // Gera 64 bytes aleatórios criptograficamente seguros
        // RandomNumberGenerator é o gerador seguro — NÃO use Random.Shared para segurança
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var plaintext = Convert.ToBase64String(randomBytes);

        // Hash SHA-256 para salvar no banco — nunca salvar o plaintext
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return (plaintext, hash);
    }

    public Guid? GetUserIdFromExpiredToken(string accessToken)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secret = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret não configurado.");

        var tokenValidationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            // IMPORTANTE: não validar expiração aqui — é exatamente isso que queremos
            ValidateLifetime = false,
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(accessToken, tokenValidationParams, out var validatedToken);

            // Verifica que o algoritmo é o esperado — evitar "alg:none" attack
            if (validatedToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
                return null;

            var userIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
        catch
        {
            // Token malformado ou assinatura inválida — retorna null
            return null;
        }
    }
}
