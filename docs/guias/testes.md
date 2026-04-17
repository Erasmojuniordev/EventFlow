# Guia de Testes — Qualidade de Software com .NET

Testes automatizados são a diferença entre "acho que funciona" e "tenho certeza que funciona". Este guia cobre tipos de teste, ferramentas, o que testar e como o EventFlow aplica cada conceito.

---

## 1. A Pirâmide de Testes

```
         /\
        /  \
       / E2E \        ← poucos, lentos, frágeis
      /--------\
     /Integration\    ← médio número, moderados
    /--------------\
   /   Unit Tests   \ ← muitos, rápidos, isolados
  /------------------\
```

| Tipo | Velocidade | Custo | O que testa | EventFlow |
|---|---|---|---|---|
| **Unit** | ms | Baixo | Lógica isolada | Domain.Tests + Application.Tests |
| **Integration** | segundos | Médio | Camadas juntas + banco real | API.Tests (TestContainers) |
| **E2E** | minutos | Alto | Fluxo completo (browser) | Fase futura (Playwright) |

**Regra geral:** 70% unit, 20% integration, 10% E2E.

---

## 2. Testes Unitários

### O que é?
Testa **uma unidade de código isolada** — uma função, método, ou classe — sem dependências externas.

### Características
- Sem banco de dados
- Sem HTTP
- Sem arquivos
- Dependências substituídas por mocks
- Resultado determinístico (mesmo input = mesmo output sempre)

### xUnit no .NET

```csharp
public class TicketTests
{
    [Fact]  // teste sem parâmetros
    public void Cancel_WhenReserved_ShouldChangeToCancelled()
    {
        // Arrange — preparar
        var ticket = Ticket.Create(Guid.NewGuid(), Guid.NewGuid());

        // Act — executar
        ticket.Cancel();

        // Assert — verificar
        ticket.Status.Should().Be(TicketStatus.Cancelled);
    }

    [Theory]  // teste parametrizado — roda para cada InlineData
    [InlineData(TicketStatus.Reserved)]
    [InlineData(TicketStatus.Confirmed)]
    public void Cancel_ActiveTicket_ShouldSucceed(TicketStatus initialStatus)
    {
        var ticket = CreateTicketInStatus(initialStatus);
        ticket.Invoking(t => t.Cancel()).Should().NotThrow();
    }
}
```

### Nomenclatura de Testes

**Padrão:** `MetodoTestado_EstadoOuCenario_ResultadoEsperado`

```csharp
// ✅ Descritivo — qual cenário, qual resultado
Cancel_WhenTicketIsUsed_ShouldThrowDomainException()
Publish_WhenEventIsDraft_AndDateIsFuture_ShouldChangeStatusToPublished()
BookTicket_WhenEventIsFull_ShouldCreateWaitlistEntry()

// ❌ Vago — não diz o que está sendo testado
TestCancel()
TestTicket_1()
Should_Work()
```

### FluentAssertions — Asserções Legíveis

```csharp
// Sem FluentAssertions (padrão xUnit)
Assert.Equal(TicketStatus.Cancelled, ticket.Status);
Assert.True(ticket.DomainEvents.Any());
Assert.Throws<DomainException>(() => ticket.Cancel());

// Com FluentAssertions — lê como frase em inglês
ticket.Status.Should().Be(TicketStatus.Cancelled);
ticket.DomainEvents.Should().HaveCount(1);
ticket.Invoking(t => t.Cancel()).Should().Throw<DomainException>()
    .WithMessage("*cancelado*");

// Coleções
list.Should().HaveCount(3);
list.Should().Contain(item => item.Id == expectedId);
list.Should().BeEmpty();
list.Should().BeInAscendingOrder(x => x.Position);
```

---

## 3. Mocks com Moq

### O que é um Mock?
Um objeto falso que substitui uma dependência real nos testes unitários.

```csharp
// Sem mock: precisaria de banco real para testar o handler
var handler = new BookTicketCommandHandler(
    new EventRepository(realDbContext),   // precisa de banco!
    new TicketRepository(realDbContext),
    ...
);

// Com mock: teste rápido, sem banco
var eventRepoMock = new Mock<IEventRepository>();
var handler = new BookTicketCommandHandler(
    eventRepoMock.Object,   // objeto falso controlado pelo teste
    ...
);
```

### Configurar comportamento

```csharp
// Setup: definir o que o mock retorna
eventRepoMock
    .Setup(r => r.GetByIdTrackedAsync(eventId, It.IsAny<CancellationToken>()))
    .ReturnsAsync(publishedEvent);

// It.IsAny<T>() = aceita qualquer valor daquele tipo
// It.Is<T>(x => x > 0) = aceita valor que satisfaz a condição

// Verificar que foi chamado
eventRepoMock.Verify(
    r => r.GetByIdTrackedAsync(eventId, It.IsAny<CancellationToken>()),
    Times.Once);  // deve ter sido chamado exatamente uma vez

// Verificar que NÃO foi chamado
unitOfWorkMock.Verify(
    u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
    Times.Never);  // não deve ter salvado nada
```

### Quando NÃO usar mocks

```csharp
// ❌ Mockar entidades do domínio — teste frágil, não testa a lógica real
var ticketMock = new Mock<Ticket>();
ticketMock.Setup(t => t.Status).Returns(TicketStatus.Cancelled);

// ✅ Usar a entidade real — testa a lógica de negócio de verdade
var ticket = Ticket.Create(eventId, attendeeId);
ticket.Cancel();
ticket.Status.Should().Be(TicketStatus.Cancelled);
```

**Regra:** Mock o que é infra (repositórios, serviços externos). Use real o que é domínio.

---

## 4. Testes de Integração com TestContainers

### Por que TestContainers?

| Abordagem | Pro | Contra |
|---|---|---|
| InMemory (EF) | Rápido, sem Docker | Não tem SQL real, sem FK constraints, sem índices |
| SQLite | Mais próximo do SQL real | Não suporta PostgreSQL-específico (xmin, tipos) |
| **TestContainers** | Banco PostgreSQL real | Precisa do Docker, mais lento (~10s) |

**Para o EventFlow:** sem TestContainers, o teste de concorrência com `xmin` seria impossível.

### Setup no EventFlow

```csharp
// 1. Sobe o container antes dos testes
private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
    .WithImage("postgres:16-alpine")
    .Build();

// 2. Substitui a connection string da aplicação
builder.ConfigureServices(services => {
    services.RemoveDbContext<AppDbContext>();
    services.AddDbContext<AppDbContext>(opts =>
        opts.UseNpgsql(_db.GetConnectionString()));
});

// 3. Aplica migrations no banco de teste
await db.Database.MigrateAsync();

// 4. Limpa dados entre testes
await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE tickets CASCADE");
```

### WebApplicationFactory

```csharp
// Sobe a aplicação inteira em memória para testes HTTP
public class MyFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services => {
            // trocar banco, substituir serviços externos, etc.
        });
    }
}

// No teste:
var client = factory.CreateClient();
var response = await client.PostAsJsonAsync("/api/auth/login", credentials);
response.StatusCode.Should().Be(HttpStatusCode.OK);
```

---

## 5. O Que Testar em Cada Camada

### Domain.Tests — Lógica Pura

```
✅ Testar:
- Transições de estado (máquinas de estado)
- Validações de invariantes
- Cálculos e derivações (AvailableSpots, IsFull)
- Domain Events emitidos

❌ Não testar:
- Integração com banco
- Serialização
- Controllers
```

### Application.Tests — Orquestração

```
✅ Testar:
- Lógica do handler (qual serviço é chamado em qual cenário)
- Autorização baseada em recurso
- Respostas de erro corretas (NotFound, Forbidden, Conflict)
- Que o UnitOfWork é chamado (SaveChanges)

❌ Não testar:
- A entidade em si (isso é do Domain.Tests)
- HTTP (isso é do API.Tests)
```

### API.Tests — Contrato HTTP + Integração

```
✅ Testar:
- Status HTTP correto (200, 201, 400, 401, 403, 404, 422)
- Formato de resposta (JSON correto)
- Fluxos completos ponta-a-ponta
- Concorrência real (múltiplos requests simultâneos)
- Autorização de rota (roles corretas)
- Middleware (ExceptionMiddleware, Rate Limiting)

❌ Não testar:
- Lógica que já está nos testes unitários
- Cada cenário de negócio (isso é do Application.Tests)
```

---

## 6. Isolamento entre Testes

### Problema: testes que interferem entre si

```csharp
// ❌ Teste A cria um usuário com o email "teste@email.com"
// ❌ Teste B também tenta criar esse usuário → falha porque já existe
// → Testes dependem da ordem de execução (flaky tests)
```

### Solução: limpar o banco entre testes

```csharp
// IAsyncLifetime garante que InitializeAsync é chamado antes de cada teste
public class MeusTestes : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
```

### Alternativa: transaction rollback

```csharp
// Mais rápido que TRUNCATE, mas mais complexo de implementar
// Cada teste roda dentro de uma transação que é revertida ao final
```

---

## 7. Cobertura de Código (Coverage)

```bash
# Rodar testes com cobertura
dotnet test --collect:"XPlat Code Coverage"

# Gerar relatório HTML
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report"
```

**Cuidado com a métrica de cobertura:**
- 80% de cobertura não significa 80% dos bugs encontrados
- Código pode ser "coberto" por um teste que não verifica nada
- Prefira testes significativos a testes que apenas inflam a cobertura

---

## 8. Testes de Contrato e E2E (Próximas Fases)

**Playwright (E2E):** controla um browser real para testar o frontend + API juntos.
```csharp
// Simula um usuário real clicando na interface
await page.GotoAsync("http://localhost:5173");
await page.FillAsync("[name=email]", "user@test.com");
await page.ClickAsync("button[type=submit]");
await page.WaitForURLAsync("**/dashboard");
```

**PactNet (Contract Tests):** garante que API e frontend concordam no contrato (formato de JSON).

---

## 9. O que Estudar a Seguir

| Tópico | Por que estudar |
|---|---|
| Teoria da pirâmide de testes | Mike Cohn — "Succeeding with Agile" |
| BDD com SpecFlow | Testes escritos em linguagem natural |
| Mutation Testing (Stryker) | Verifica se os testes realmente testam algo |
| Playwright (.NET) | Testes E2E automatizados |
| Testes de performance (k6, NBomber) | Simular carga em produção |
| TestContainers avançado | Múltiplos serviços (Redis, RabbitMQ) |

---

## 10. O que Evitar

| Prática ruim | Consequência | O que fazer |
|---|---|---|
| Testar detalhes de implementação | Teste quebra ao refatorar sem mudar comportamento | Testar comportamento observável |
| Mock de tudo | Testa o mock, não o código real | Mocks só para dependências de infra |
| Testes sem Assert | "Testes" que nunca falham | Todo teste deve ter pelo menos um `.Should()` |
| Banco compartilhado entre testes | Flaky tests, dependência de ordem | Limpar banco antes de cada teste |
| Ignorar testes que falham (`[Skip]`) | Débito técnico de testes | Corrigir o teste ou o código |
| Testar só o "caminho feliz" | Bugs nos cenários de erro | Testar caminhos de erro e edge cases |
| Nomes de teste vagos (`Test1`, `TestMethod`) | Impossível entender o que falhou | `Método_Cenário_ResultadoEsperado` |
