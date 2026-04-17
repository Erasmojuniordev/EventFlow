# CLAUDE.md — EventFlow

## PROJETO
EventFlow — Plataforma de gestão de eventos e ingressos.
Stack: .NET 8 (Clean Architecture) + React 18 + TypeScript + Tailwind CSS + PostgreSQL.

## CONTEXTO DO DESENVOLVEDOR
Nível estagiário/junior em .NET. Boa base em POO, Clean Architecture e EF Core.
Aprendendo: segurança aprofundada, testes unitários/integração, logs estruturados.
SEMPRE comentar decisões não-óbvias com: O que é, Por quê, Alternativas, Armadilhas.
Este projeto é uma ferramenta de aprendizado guiado — comentários didáticos são obrigatórios.

## IDIOMA
Comentários, commits e mensagens de erro em PORTUGUÊS.
Código (nomes de variáveis, funções, classes) em INGLÊS.

## ARQUITETURA BACKEND — CLEAN ARCHITECTURE
4 camadas com dependência de fora para dentro:
  API → Application → Domain ← Infrastructure

### EventFlow.Domain (ZERO dependências externas)
- Entidades: BaseEntity (Id Guid, CreatedAt, UpdatedAt)
- Event, Ticket, WaitlistEntry
- Enums: EventStatus, TicketStatus, UserRole (constantes string)
- Domain Events: IDomainEvent, TicketCancelled, WaitlistEntryPromoted
- Interfaces de repositório: IEventRepository, ITicketRepository, IWaitlistRepository
- Exceções: DomainException (violações de regra de negócio/invariante)
- Regras de negócio PURAS nas entidades (entidades "autoprotetoras")

### EventFlow.Application (depende só do Domain)
- Pacotes: MediatR, FluentValidation
- Estrutura: Features/{Feature}/Commands/, Queries/, Validators/, DTOs/
- Cada use case = 1 Request (IRequest<Result<T>>) + 1 Handler
- Result<T> pattern: NUNCA exceptions para fluxo de controle
- AuthResultWithRefreshToken: subclasse de Result<AuthResponse> para carregar o refresh token
- Pipeline Behaviors: ValidationBehavior (valida antes do handler) + LoggingBehavior (loga tudo)
- Interfaces de serviço: ITokenService, IIdentityService, IRefreshTokenRepository, ICurrentUser

### EventFlow.Infrastructure
- Pacotes: Npgsql EF Core 8, Identity, Serilog 8, JWT Bearer 8
- AppDbContext herda IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
- ApplicationUser herda IdentityUser<Guid> (campo Name + CreatedAt)
- RefreshToken: salva hash SHA-256, nunca plaintext
- AuditInterceptor: atualiza UpdatedAt automaticamente via SaveChangesInterceptor
- Configurações EF via IEntityTypeConfiguration<T> — snake_case no PostgreSQL
- Enum salvo como string no banco (não int) — mais legível e resiliente a reordenação

### EventFlow.API
- Controllers THIN: request → MediatR → mapeia Result → HTTP response
- ExceptionMiddleware: captura ValidationException (400), DomainException (422), Exception (500)
- CorrelationIdMiddleware: X-Correlation-ID em todas as requests
- Rate limiting: Fixed Window 5/min no endpoint de login
- Refresh token: httpOnly cookie (nunca retornado no body JSON)
- CORS: frontend em localhost:5173

## PADRÕES BACKEND
- PascalCase. Interfaces com I. 1 classe por arquivo.
- Async/await em TODO I/O. Sufixo Async.
- DI via construtor. Nunca service locator.
- Secrets via Environment Variables / user-secrets. NUNCA appsettings.json.
- Datas UTC no banco. Timezone no frontend.
- Enum como string no banco — nunca int.

## SEGURANÇA
- JWT HS256, access token 15min, refresh token 7 dias
- Refresh token: plaintext enviado em cookie httpOnly SameSite=Strict
- Hash SHA-256 do refresh token salvo no banco
- Lockout: 5 tentativas, 15min bloqueio
- User enumeration: mensagem genérica "Credenciais inválidas" no login

## TESTES
- Domain.Tests: xUnit + FluentAssertions — sem dependências externas, puramente unitários
- Application.Tests: xUnit + Moq + FluentAssertions — mock de repositórios
- API.Tests: WebApplicationFactory + TestContainers PostgreSQL real
- Nomenclatura: MethodName_StateUnderTest_ExpectedBehavior
- Padrão AAA: Arrange / Act / Assert

## FASES DE DESENVOLVIMENTO
- Fase 1 (atual): Fundação + Autenticação ← VOCÊ ESTÁ AQUI
- Fase 2: Gestão de Eventos (CQRS completo, policies de autorização)
- Fase 3: Ingressos + Regras de Negócio (Domain Events, concorrência otimista)
- Fase 4: Testes Abrangentes (TestContainers, cobertura completa)
- Fase 5: QR Code + Check-in (HMAC assinado, replay attack prevention)
- Fase 6: Dashboard + Refinamentos (health checks, métricas, frontend gráficos)

## PRÓXIMOS PASSOS IMEDIATOS
1. Gerar migration inicial: cd src/EventFlow.API && dotnet ef migrations add InitialCreate -p ../EventFlow.Infrastructure
2. Subir banco: docker-compose up -d
3. dotnet run (configura user-secrets antes: JwtSettings:Secret e ConnectionStrings:DefaultConnection)
4. Testar no Swagger: POST /api/auth/register → POST /api/auth/login
