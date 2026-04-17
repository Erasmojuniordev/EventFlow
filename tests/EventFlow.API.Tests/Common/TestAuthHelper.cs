using System.Net.Http.Json;
using System.Text.Json;

namespace EventFlow.API.Tests.Common;

/// <summary>
/// Helpers para autenticação nos testes de integração.
///
/// POR QUÊ um helper separado e não repetir o login em cada teste?
/// - DRY: evita duplicação de código de setup
/// - Clareza: o corpo do teste foca no que está sendo testado, não no setup de auth
/// - Manutenção: se o endpoint de login mudar, corrige em um só lugar
/// </summary>
public static class TestAuthHelper
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Registra um novo usuário e retorna o token JWT de acesso.
    /// Útil para criar usuários de teste com roles específicas.
    /// </summary>
    public static async Task<string> RegisterAndGetTokenAsync(
        HttpClient client,
        string email,
        string password = "Senha@123",
        string name = "Usuário Teste",
        string role = "Attendee")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            name,
            email,
            password,
            role
        });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    /// <summary>
    /// Faz login com usuário existente e retorna o token JWT.
    /// </summary>
    public static async Task<string> LoginAndGetTokenAsync(
        HttpClient client,
        string email,
        string password = "Senha@123")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password
        });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    /// <summary>
    /// Cria um HttpClient com o header Authorization já configurado.
    /// Simplifica testes que precisam de um cliente autenticado.
    /// </summary>
    public static void SetBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }
}
