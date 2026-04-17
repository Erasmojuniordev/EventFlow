using EventFlow.API.Tests.Common;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace EventFlow.API.Tests.Controllers;

/// <summary>
/// Testes de integração do EventsController.
///
/// FOCO:
/// - Fluxo completo: criar → publicar → listar
/// - Autorização baseada em role (só Organizer cria eventos)
/// - Autorização baseada em recurso (Organizer só edita os próprios eventos)
/// - Paginação na listagem
///
/// CONVENÇÃO DE DADOS DE TESTE:
/// Datas futuras usam AddDays(30)/AddDays(31) para evitar falhas por "data no passado".
/// Não usar datas fixas — testes de data quebram com o tempo.
/// </summary>
public class EventsControllerTests(EventFlowWebApplicationFactory factory)
    : IClassFixture<EventFlowWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static object CreateEventPayload(string title = "Evento Teste") => new
    {
        title,
        description = "Descrição do evento de teste",
        location = "São Paulo, SP",
        startsAt = DateTime.UtcNow.AddDays(30).ToString("O"),
        endsAt = DateTime.UtcNow.AddDays(31).ToString("O"),
        capacity = 100
    };

    [Fact]
    public async Task CreateEvent_AsOrganizer_ShouldReturn201WithEventId()
    {
        // Arrange
        var token = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "org@teste.com", role: "Organizer");
        _client.SetBearerToken(token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/events", CreateEventPayload());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("status").GetString().Should().Be("Draft");
    }

    [Fact]
    public async Task CreateEvent_AsAttendee_ShouldReturn403()
    {
        // Arrange: Attendee não tem permissão para criar eventos
        var token = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "attendee@teste.com", role: "Attendee");
        _client.SetBearerToken(token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/events", CreateEventPayload());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateEvent_WithoutAuth_ShouldReturn401()
    {
        var response = await _client.PostAsJsonAsync("/api/events", CreateEventPayload());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PublishEvent_AsOwner_ShouldChangeStatusToPublished()
    {
        // Arrange: criar e depois publicar
        var token = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "org@teste.com", role: "Organizer");
        _client.SetBearerToken(token);

        var createResponse = await _client.PostAsJsonAsync("/api/events", CreateEventPayload());
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var eventId = createBody.GetProperty("id").GetString();

        // Act
        var publishResponse = await _client.PostAsync($"/api/events/{eventId}/publish", null);

        // Assert
        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var publishBody = await publishResponse.Content.ReadFromJsonAsync<JsonElement>();
        publishBody.GetProperty("status").GetString().Should().Be("Published");
    }

    [Fact]
    public async Task UpdateEvent_AsDifferentOrganizer_ShouldReturn403()
    {
        // Arrange: Organizer A cria o evento
        var orgAToken = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "orgA@teste.com", role: "Organizer");
        _client.SetBearerToken(orgAToken);

        var createResponse = await _client.PostAsJsonAsync("/api/events", CreateEventPayload());
        var body = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var eventId = body.GetProperty("id").GetString();

        // Act: Organizer B tenta editar o evento de A
        var orgBToken = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "orgB@teste.com", role: "Organizer");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", orgBToken);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/events/{eventId}", CreateEventPayload("Evento Modificado por B"));

        // Assert: resource-based auth — 403 porque B não é dono do evento de A
        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetEvents_Published_ShouldReturnOnlyPublishedEvents()
    {
        // Arrange: criar um Draft e um Published
        var orgToken = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "org@teste.com", role: "Organizer");
        _client.SetBearerToken(orgToken);

        // Evento 1: fica em Draft
        await _client.PostAsJsonAsync("/api/events", CreateEventPayload("Draft Evento"));

        // Evento 2: publicado
        var create2 = await _client.PostAsJsonAsync("/api/events", CreateEventPayload("Evento Publicado"));
        var body2 = await create2.Content.ReadFromJsonAsync<JsonElement>();
        var eventId2 = body2.GetProperty("id").GetString();
        await _client.PostAsync($"/api/events/{eventId2}/publish", null);

        // Act: GET /api/events (público — não precisa de token)
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/events");

        // Assert: apenas o Published aparece
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = listBody.GetProperty("items").EnumerateArray().ToList();

        items.Should().HaveCount(1);
        items[0].GetProperty("title").GetString().Should().Be("Evento Publicado");
    }

    [Fact]
    public async Task GetEventById_DraftEvent_AsAttendee_ShouldReturn404()
    {
        // Arrange: criar evento em Draft
        var orgToken = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "org@teste.com", role: "Organizer");
        _client.SetBearerToken(orgToken);

        var createResponse = await _client.PostAsJsonAsync("/api/events", CreateEventPayload());
        var body = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var eventId = body.GetProperty("id").GetString();

        // Act: Attendee tenta ver o evento em Draft
        var attendeeToken = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "attendee@teste.com", role: "Attendee");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", attendeeToken);

        var response = await _client.GetAsync($"/api/events/{eventId}");

        // Assert: 404 em vez de 403 — evita information disclosure (o attendee não sabe que existe)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
