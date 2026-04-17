using EventFlow.API.Tests.Common;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace EventFlow.API.Tests.Controllers;

/// <summary>
/// Testes de integração do TicketsController.
///
/// ESTE É O ARQUIVO MAIS IMPORTANTE DO PROJETO DE TESTES.
/// Aqui validamos regras que só podem ser testadas com banco real:
///
/// 1. FLUXO COMPLETO: Book → Confirm → Cancel → Promoção da fila
/// 2. CONCORRÊNCIA REAL: dois requests simultâneos para o último ingresso
///    — este teste PROVA que o mecanismo de xmin funciona em produção
///    — impossível testar com mocks (precisaria de dois DbContexts reais)
///
/// Por que TestContainers é indispensável aqui?
/// - InMemory: não tem xmin, não detecta conflitos
/// - SQLite: não tem xmin, ignora a coluna "xid"
/// - PostgreSQL real: o xmin funciona exatamente como em produção
/// </summary>
public class TicketsControllerTests(EventFlowWebApplicationFactory factory)
    : IClassFixture<EventFlowWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Helper que cria e publica um evento retornando seu ID.</summary>
    private async Task<string> CreateAndPublishEventAsync(string orgToken, int capacity = 10)
    {
        _client.SetBearerToken(orgToken);

        var createResponse = await _client.PostAsJsonAsync("/api/events", new
        {
            title = "Evento para Ingressos",
            description = "Teste de reserva",
            location = "Online",
            startsAt = DateTime.UtcNow.AddDays(30).ToString("O"),
            endsAt = DateTime.UtcNow.AddDays(31).ToString("O"),
            capacity
        });

        createResponse.EnsureSuccessStatusCode();
        var body = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var eventId = body.GetProperty("id").GetString()!;

        var publishResponse = await _client.PostAsync($"/api/events/{eventId}/publish", null);
        publishResponse.EnsureSuccessStatusCode();

        return eventId;
    }

    [Fact]
    public async Task BookTicket_AsAttendee_ShouldReturn201WithReservedTicket()
    {
        // Arrange
        var orgToken = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "org@teste.com", role: "Organizer");
        var eventId = await CreateAndPublishEventAsync(orgToken, capacity: 10);

        var attendeeToken = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "attendee@teste.com", role: "Attendee");
        _client.SetBearerToken(attendeeToken);

        // Act
        var response = await _client.PostAsync($"/api/events/{eventId}/tickets", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isTicket").GetBoolean().Should().BeTrue();
        body.GetProperty("ticket").GetProperty("status").GetString().Should().Be("Reserved");
    }

    [Fact]
    public async Task BookTicket_WhenEventIsFull_ShouldReturn200WithWaitlistPosition()
    {
        // Arrange: evento com capacity=1
        var orgToken = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "org@teste.com", role: "Organizer");
        var eventId = await CreateAndPublishEventAsync(orgToken, capacity: 1);

        // Primeiro attendee pega o único ingresso
        var attendee1Token = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "a1@teste.com", role: "Attendee");
        _client.SetBearerToken(attendee1Token);
        var firstBooking = await _client.PostAsync($"/api/events/{eventId}/tickets", null);
        firstBooking.StatusCode.Should().Be(HttpStatusCode.Created);

        // Segundo attendee vai para a fila
        var attendee2Token = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "a2@teste.com", role: "Attendee");
        _client.SetBearerToken(attendee2Token);

        // Act
        var response = await _client.PostAsync($"/api/events/{eventId}/tickets", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);  // 200 = waitlist (não 201 = criado)

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isTicket").GetBoolean().Should().BeFalse();
        body.GetProperty("waitlistPosition").GetProperty("position").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task CancelTicket_ShouldTriggerWaitlistPromotion()
    {
        // Arrange: evento capacity=1
        var orgToken = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "org@teste.com", role: "Organizer");
        var eventId = await CreateAndPublishEventAsync(orgToken, capacity: 1);

        // Attendee 1 reserva o único ingresso
        var a1Token = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "a1@teste.com", role: "Attendee");
        _client.SetBearerToken(a1Token);
        var booking1Response = await _client.PostAsync($"/api/events/{eventId}/tickets", null);
        var booking1Body = await booking1Response.Content.ReadFromJsonAsync<JsonElement>();
        var ticketId = booking1Body.GetProperty("ticket").GetProperty("id").GetString()!;

        // Attendee 2 entra na fila
        var a2Token = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "a2@teste.com", role: "Attendee");
        _client.SetBearerToken(a2Token);
        await _client.PostAsync($"/api/events/{eventId}/tickets", null);

        // Act: Attendee 1 cancela o ingresso → deve promover Attendee 2 da fila
        _client.SetBearerToken(a1Token);
        var cancelResponse = await _client.DeleteAsync($"/api/tickets/{ticketId}");

        // Assert
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verificar que Attendee 2 agora tem um ingresso (foi promovido da fila)
        _client.SetBearerToken(a2Token);
        var myTicketsResponse = await _client.GetAsync("/api/tickets/mine");
        myTicketsResponse.EnsureSuccessStatusCode();

        var myTickets = await myTicketsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var tickets = myTickets.EnumerateArray().ToList();

        // Attendee 2 deve ter um ingresso Reserved (promovido da fila)
        tickets.Should().HaveCount(1);
        tickets[0].GetProperty("status").GetString().Should().Be("Reserved");
    }

    [Fact]
    public async Task ConfirmTicket_ShouldGenerateTicketCode()
    {
        // Arrange
        var orgToken = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "org@teste.com", role: "Organizer");
        var eventId = await CreateAndPublishEventAsync(orgToken);

        var attendeeToken = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "att@teste.com", role: "Attendee");
        _client.SetBearerToken(attendeeToken);

        var bookResponse = await _client.PostAsync($"/api/events/{eventId}/tickets", null);
        var bookBody = await bookResponse.Content.ReadFromJsonAsync<JsonElement>();
        var ticketId = bookBody.GetProperty("ticket").GetProperty("id").GetString()!;

        // TicketCode deve ser null antes de confirmar
        bookBody.GetProperty("ticket").GetProperty("ticketCode").ValueKind
            .Should().Be(JsonValueKind.Null);

        // Act
        var confirmResponse = await _client.PostAsync($"/api/tickets/{ticketId}/confirm", null);

        // Assert
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var confirmBody = await confirmResponse.Content.ReadFromJsonAsync<JsonElement>();
        confirmBody.GetProperty("status").GetString().Should().Be("Confirmed");
        confirmBody.GetProperty("ticketCode").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CancelTicket_OtherUsersTicket_ShouldReturn403()
    {
        // Arrange
        var orgToken = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "org@teste.com", role: "Organizer");
        var eventId = await CreateAndPublishEventAsync(orgToken);

        var ownerToken = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "owner@teste.com", role: "Attendee");
        _client.SetBearerToken(ownerToken);
        var bookResponse = await _client.PostAsync($"/api/events/{eventId}/tickets", null);
        var bookBody = await bookResponse.Content.ReadFromJsonAsync<JsonElement>();
        var ticketId = bookBody.GetProperty("ticket").GetProperty("id").GetString()!;

        // Act: outro attendee tenta cancelar o ingresso do owner
        var intruderToken = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "intruder@teste.com", role: "Attendee");
        _client.SetBearerToken(intruderToken);

        var response = await _client.DeleteAsync($"/api/tickets/{ticketId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// TESTE DE CONCORRÊNCIA — o mais importante do projeto.
    ///
    /// CENÁRIO: evento com capacity=1, dois attendees tentando reservar simultaneamente.
    ///
    /// RESULTADO ESPERADO:
    /// - Exatamente um dos dois recebe um ingresso (201 Created, isTicket=true)
    /// - O outro entra na fila de espera (200 OK, isTicket=false) OU também recebe 201
    ///   (se o Polly retry encontrou vaga na segunda tentativa — improvável com capacity=1)
    ///
    /// COMO ESTE TESTE PROVA QUE O MECANISMO FUNCIONA:
    /// Sem o xmin + Polly retry, AMBOS poderiam receber 201 e criar 2 tickets para
    /// um evento com capacity=1 — violando a regra de negócio.
    /// Com o mecanismo, apenas um persiste; o outro é retentado e vai para a fila.
    ///
    /// NOTA: Testes de concorrência têm comportamento não-determinístico.
    /// Não testamos qual dos dois venceu — só que a invariante foi mantida
    /// (totalTickets + totalWaitlist = 2, e totalTickets <= 1).
    /// </summary>
    [Fact]
    public async Task BookTicket_Concurrent_ShouldRespectCapacityInvariant()
    {
        // Arrange: evento com exatamente 1 vaga
        var orgToken = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "org@teste.com", role: "Organizer");
        var eventId = await CreateAndPublishEventAsync(orgToken, capacity: 1);

        // Dois attendees com seus próprios clientes HTTP (conexões independentes)
        var client1 = factory.CreateClient();
        var client2 = factory.CreateClient();

        var a1Token = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "a1@teste.com", role: "Attendee");
        var a2Token = await TestAuthHelper.RegisterAndGetTokenAsync(
            _client, "a2@teste.com", role: "Attendee");

        client1.SetBearerToken(a1Token);
        client2.SetBearerToken(a2Token);

        // Act: os dois tentam reservar ao mesmo tempo
        var task1 = client1.PostAsync($"/api/events/{eventId}/tickets", null);
        var task2 = client2.PostAsync($"/api/events/{eventId}/tickets", null);

        var responses = await Task.WhenAll(task1, task2);

        // Assert: ambos devem ter recebido resposta bem-sucedida (200 ou 201)
        responses[0].IsSuccessStatusCode.Should().BeTrue();
        responses[1].IsSuccessStatusCode.Should().BeTrue();

        // Contabilizar: tickets reais vs fila de espera
        var bodies = await Task.WhenAll(
            responses[0].Content.ReadFromJsonAsync<JsonElement>(),
            responses[1].Content.ReadFromJsonAsync<JsonElement>());

        var ticketCount = bodies.Count(b => b.GetProperty("isTicket").GetBoolean());
        var waitlistCount = bodies.Count(b => !b.GetProperty("isTicket").GetBoolean());

        // INVARIANTE: no máximo 1 ingresso para um evento com capacity=1
        ticketCount.Should().BeLessThanOrEqualTo(1,
            "um evento com capacity=1 não pode ter mais de 1 ingresso");

        // INVARIANTE: todos os requests foram tratados (1 ingresso + 1 fila, ou 2 na fila se retry)
        (ticketCount + waitlistCount).Should().Be(2,
            "ambos os attendees devem ter recebido uma resposta (ingresso ou fila)");
    }
}
