using EventFlow.API.Tests.Common;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace EventFlow.API.Tests.Controllers;

/// <summary>
/// Testes de integração do AuthController.
///
/// DIFERENÇA DOS TESTES UNITÁRIOS:
/// - Unitários: mockam repositórios, testam lógica isolada do handler
/// - Integração: testam o fluxo COMPLETO — HTTP request → middleware → handler → banco real → HTTP response
///
/// O QUE ESSES TESTES VERIFICAM QUE OS UNITÁRIOS NÃO CONSEGUEM:
/// - Rota correta (/api/auth/register)
/// - Serialização/Desserialização JSON
/// - Status HTTP correto
/// - Middleware de exceção retorna ProblemDetails correto
/// - FluentValidation pipeline integrado
/// - Persistência no banco real (não mock)
///
/// IClassFixture<T>: compartilha uma instância da factory com todos os testes desta classe.
/// O container PostgreSQL sobe uma vez e é reusado — mais rápido que um container por teste.
/// </summary>
public class AuthControllerTests(EventFlowWebApplicationFactory factory)
    : IClassFixture<EventFlowWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    // IAsyncLifetime.InitializeAsync: chamado antes de CADA teste
    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    // IAsyncLifetime.DisposeAsync: cleanup após cada teste (não precisamos de nada aqui)
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_WithValidData_ShouldReturn200WithAccessToken()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "João Silva",
            email = "joao@teste.com",
            password = "Senha@123",
            role = "Attendee"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("user").GetProperty("email").GetString().Should().Be("joao@teste.com");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturn400()
    {
        // Arrange: registrar o primeiro usuário
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Primeiro",
            email = "duplicado@teste.com",
            password = "Senha@123",
            role = "Attendee"
        });

        // Act: tentar registrar com o mesmo email
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Segundo",
            email = "duplicado@teste.com",
            password = "Senha@123",
            role = "Attendee"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithInvalidPassword_ShouldReturn400WithValidationErrors()
    {
        // Arrange: senha sem maiúscula, número ou caractere especial
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Teste",
            email = "teste@teste.com",
            password = "senhasimples",
            role = "Attendee"
        });

        // Assert: FluentValidation pipeline → 400
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturn200WithTokens()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Attendee",
            email = "login@teste.com",
            password = "Senha@123",
            role = "Attendee"
        });

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "login@teste.com",
            password = "Senha@123"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();

        // Cookie httpOnly do refresh token deve estar presente
        response.Headers.Should().ContainKey("Set-Cookie");
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShouldReturn401WithGenericMessage()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "User",
            email = "user@teste.com",
            password = "Senha@123",
            role = "Attendee"
        });

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "user@teste.com",
            password = "SenhaErrada@123"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // SEGURANÇA: verifica que a mensagem é genérica (anti user enumeration)
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Credenciais inválidas");
        body.Should().NotContain("usuário não encontrado");
        body.Should().NotContain("senha incorreta");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ShouldReturn401()
    {
        // Act: sem Authorization header
        var response = await _client.GetAsync("/api/events/mine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_ShouldNotReturn401()
    {
        // Arrange
        var token = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "organizer@teste.com", role: "Organizer");
        _client.SetBearerToken(token);

        // Act
        var response = await _client.GetAsync("/api/events/mine");

        // Assert: pode ser 200 (OK) com lista vazia — o importante é não ser 401
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}
