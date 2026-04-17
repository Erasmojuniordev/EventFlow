# Guia de Segurança — Aplicações Web .NET

Segurança não é uma feature adicionada no final — é construída durante o desenvolvimento. Este guia cobre as ameaças mais comuns, o que o EventFlow já protege, e o que estudar.

---

## 1. OWASP Top 10 — As 10 Vulnerabilidades Mais Comuns

A OWASP (Open Worldwide Application Security Project) mantém a lista das vulnerabilidades mais críticas. Todo desenvolvedor web deve conhecê-las.

### A01 — Quebra de Controle de Acesso
**O que é:** Usuário acessa recursos que não deveria.

```csharp
// ❌ Vulnerável: qualquer usuário autenticado pode cancelar qualquer ingresso
[HttpDelete("/api/tickets/{id}")]
[Authorize]
public async Task<IActionResult> CancelTicket(Guid id)
{
    await _ticketService.CancelAsync(id); // não verifica se é do usuário logado!
}

// ✅ EventFlow faz isso corretamente (resource-based auth no handler):
if (ticket.AttendeeId != currentUser.Id && !currentUser.IsInRole("Admin"))
    return Result.Forbidden("Você não tem permissão para cancelar este ingresso.");
```

### A02 — Falhas Criptográficas
**O que é:** Dados sensíveis mal protegidos (senhas em plaintext, JWT sem secret forte).

```csharp
// ❌ Nunca salvar senha em plaintext
user.Password = request.Password;

// ✅ ASP.NET Identity usa PBKDF2 automaticamente
await _userManager.CreateAsync(user, request.Password);

// ❌ Nunca salvar refresh token em plaintext
refreshToken.Token = plaintextToken;

// ✅ EventFlow salva o hash SHA-256
refreshToken.TokenHash = ComputeSha256Hash(plaintextToken);
```

### A03 — Injeção (SQL Injection, XSS, etc.)
**O que é:** Dados maliciosos executados como código.

```sql
-- ❌ SQL Injection clássico
string query = $"SELECT * FROM users WHERE email = '{email}'";
// email = "'; DROP TABLE users; --"  → desastre!

-- ✅ EF Core usa parâmetros automaticamente
var user = await db.Users.Where(u => u.Email == email).FirstOrDefaultAsync();
// Gera: SELECT * FROM users WHERE email = @p0  ← parâmetro, não interpolação
```

**No EventFlow:** EF Core protege automaticamente contra SQL Injection. Nunca use `FromSqlRaw()` com interpolação de strings.

### A04 — Design Inseguro
**O que é:** Arquitetura que facilita ataques por design.

Exemplo no EventFlow de design seguro:
- Rate limiting no login (5 req/min) → dificulta brute force
- Lockout após 5 tentativas → mitiga ataques de dicionário
- Mensagem genérica "Credenciais inválidas" → evita user enumeration

### A05 — Misconfiguration de Segurança
**O que é:** Configurações padrão inseguras deixadas em produção.

```csharp
// ❌ Swagger em produção expõe sua API
app.UseSwagger();  // sem verificar o ambiente

// ✅ EventFlow só habilita Swagger em Development
if (app.Environment.IsDevelopment())
    app.UseSwagger();
```

### A06 — Componentes Vulneráveis e Desatualizados
**O que é:** Dependências com vulnerabilidades conhecidas.

```bash
# Verificar vulnerabilidades nas dependências
dotnet list package --vulnerable

# Atualizar todos os pacotes
dotnet outdated  # requer: dotnet tool install -g dotnet-outdated-tool
```

### A07 — Falhas de Autenticação e Identificação
**O que é:** Sessões sem timeout, tokens sem expiração, senhas fracas.

```csharp
// ✅ EventFlow:
// - Access token: 15 minutos (curto prazo)
// - Refresh token: 7 dias (rotação a cada uso)
// - Lockout: 5 tentativas / 15 min
// - Cookie httpOnly (não acessível via JavaScript)
// - SameSite=Strict (proteção contra CSRF)
```

### A08 — Falhas de Integridade de Software e Dados
**O que é:** Atualizações sem verificação de assinatura, desserialização insegura.

No contexto .NET: usar apenas pacotes NuGet de fontes confiáveis, verificar hashes.

### A09 — Logging e Monitoramento Insuficientes
**O que é:** Sem logs de atividades suspeitas, sem alertas.

```csharp
// ✅ EventFlow usa Serilog com:
// - CorrelationId em todo request
// - Warning para requests lentos (>500ms) via LoggingBehavior
// - Log de cada request (SerilogRequestLogging)
// - Log de falhas de auth (Identity faz isso automaticamente)
```

### A10 — Server-Side Request Forgery (SSRF)
**O que é:** Servidor faz requests para URLs controladas pelo atacante.

Relevante quando sua API busca recursos de URLs fornecidas pelo usuário.

---

## 2. JWT — JSON Web Tokens

### Como funciona

```
Header.Payload.Signature
  ↓       ↓        ↓
base64  base64  HMAC-SHA256(header+payload, secret)
```

**O Payload é só base64 — não é criptografado!**
```bash
# Qualquer um pode decodificar o payload sem o secret:
echo "eyJzdWIiOiJ1c2VyMTIzIn0" | base64 -d
# {"sub":"user123"}
```

**Nunca colocar no payload:** senhas, números de cartão, dados que devem ser secretos.
**O que pode estar no payload:** id do usuário, roles, email (se não for sensível), expiração.

### Configurações seguras no EventFlow

```csharp
// ✅ O que o EventFlow faz corretamente:
new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,   // valida a assinatura
    ValidateIssuer = true,             // valida o emissor
    ValidateAudience = true,           // valida o público
    ValidateLifetime = true,           // valida a expiração
    ClockSkew = TimeSpan.Zero,         // sem tolerância de tempo
    // ↑ Sem isso, um token expirado há 5min ainda seria aceito (padrão do .NET)
}
```

### Tokens curtos + refresh token

```
Access Token: 15 min  →  se vazado, janela de ataque pequena
Refresh Token: 7 dias →  rotacionado a cada uso, hash no banco
Cookie httpOnly       →  JavaScript não consegue ler (XSS não rouba o token)
```

### Algoritmo: HS256 vs RS256

| | HS256 (HMAC) | RS256 (RSA) |
|---|---|---|
| Chave | Uma chave secreta compartilhada | Par de chaves pública/privada |
| Verificação | Qualquer serviço com a chave | Qualquer um com a chave pública |
| Uso | Monolito, microsserviços internos | Sistemas distribuídos, OAuth |
| EventFlow | ✅ Suficiente | Overkill para este projeto |

---

## 3. CORS — Cross-Origin Resource Sharing

```
Frontend: http://localhost:5173
API:      http://localhost:5000

Browser bloqueia por padrão: origens diferentes!
CORS diz ao browser: "essa origem é permitida"
```

**Configuração errada (perigosa):**
```csharp
// ❌ Permite qualquer origem — abre para ataques CSRF
policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();

// ❌ AllowAnyOrigin + AllowCredentials não funciona (e é inseguro)
policy.AllowAnyOrigin().AllowCredentials();
```

**Configuração correta (EventFlow):**
```csharp
policy
    .WithOrigins("http://localhost:5173", "http://localhost:3000")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials();  // necessário para cookies
```

**Em produção:** substituir pelos domínios reais exatos.

---

## 4. Injeção e Validação de Input

```csharp
// FluentValidation valida ANTES do handler processar
RuleFor(x => x.Email)
    .NotEmpty()
    .EmailAddress()
    .MaximumLength(256);

// Nunca confiar em dados do cliente sem validação
// ❌ Usar role recebida do request diretamente
var role = request.Role;  // e se alguém mandar "Admin"?

// ✅ Validar contra valores permitidos
RuleFor(x => x.Role)
    .Must(r => r == "Attendee" || r == "Organizer")
    .WithMessage("Role inválida.");
```

---

## 5. Proteção de Headers HTTP

Headers de segurança importantes:

```csharp
// Adicionar no middleware ou usar pacote NWebsec
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});
```

| Header | Protege contra |
|---|---|
| `X-Content-Type-Options: nosniff` | MIME type sniffing |
| `X-Frame-Options: DENY` | Clickjacking |
| `Content-Security-Policy` | XSS (mais complexo) |
| `Strict-Transport-Security` | Downgrade para HTTP |

---

## 6. Rate Limiting e Brute Force

```csharp
// EventFlow: 5 tentativas de login por minuto
// Após 5 tentativas com senha errada: lockout por 15 min

// Em produção, adicionar também:
// - Rate limit geral por IP
// - CAPTCHA após N tentativas
// - Notificação por email de tentativas suspeitas
```

---

## 7. Auditoria e Logs de Segurança

O que registrar (sem dados sensíveis):

```csharp
// ✅ Registrar:
Log.Warning("Tentativa de login falhou para {Email}", email);
Log.Warning("Acesso negado a recurso {ResourceId} por usuário {UserId}", id, userId);
Log.Information("Usuário {UserId} cancelou ingresso {TicketId}", userId, ticketId);

// ❌ NUNCA registrar:
Log.Information("Login com senha: {Password}", password);  // NUNCA!
Log.Information("Token gerado: {Token}", refreshToken);    // NUNCA!
```

---

## 8. O que Estudar a Seguir

| Tópico | Recurso |
|---|---|
| OWASP Top 10 (completo) | owasp.org/Top10 |
| ASP.NET Core Security | Microsoft Learn — "Segurança no ASP.NET Core" |
| Penetration Testing básico | OWASP WebGoat (app vulnerável para praticar) |
| OAuth 2.0 / OpenID Connect | Auth0 docs — excelente explicação |
| Secrets Manager | Azure Key Vault, AWS Secrets Manager |
| Análise estática (SAST) | SonarQube, GitHub CodeQL |

---

## 9. O que Evitar

| Prática | Consequência | Alternativa |
|---|---|---|
| Salvar senha em plaintext | Vazamento = todas as senhas expostas | ASP.NET Identity (PBKDF2) |
| JWT sem expiração | Token roubado = acesso eterno | Access token curto (15min) |
| AllowAnyOrigin no CORS | Ataques CSRF | Whitelist de origens específicas |
| Mensagem "usuário não encontrado" | User enumeration | Mensagem genérica sempre |
| SQL concatenado com input do usuário | SQL Injection | EF Core / parâmetros |
| Logs com senhas ou tokens | Vazamento nos logs | Nunca logar dados de auth |
| HttpOnly=false no cookie de auth | XSS rouba o token | Sempre HttpOnly=true |
| Secret JWT fraco (< 128 bits) | Brute force do secret | Mínimo 256 bits, gerado aleatoriamente |
