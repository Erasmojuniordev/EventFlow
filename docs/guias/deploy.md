# Guia de Deploy — Do Código ao Servidor

Este guia cobre o caminho que um aplicativo .NET percorre da sua máquina até um servidor na nuvem. Inclui conceitos, ferramentas, o que estudar e o que evitar.

---

## 1. O que é Deploy?

Deploy é o processo de mover seu código de um ambiente (desenvolvimento) para outro (produção), tornando-o acessível para usuários reais.

```
Seu PC → Testes → Staging → Produção
  (dev)     (CI)    (homolog)  (prod)
```

**Ambientes comuns:**

| Ambiente | Propósito | Quem usa |
|---|---|---|
| Development | Desenvolver e debugar | Você |
| Testing | Rodar testes automatizados | CI/CD pipeline |
| Staging | Validar antes de produção | QA, stakeholders |
| Production | Usuários reais | Todo mundo |

---

## 2. Formas de Deploy para .NET

### 2.1 Deploy Direto (sem container)

O jeito mais simples: publicar o binário e copiar para o servidor.

```bash
# Publicar como executável auto-contido
dotnet publish -c Release -r linux-x64 --self-contained

# Ou como framework-dependent (precisa do .NET instalado no servidor)
dotnet publish -c Release
```

**Quando usar:** VPS simples, apps pequenos, aprendizado inicial.
**Problema:** "funciona na minha máquina" — depende das versões instaladas no servidor.

---

### 2.2 Docker (containers)

O jeito profissional. Empacota o app + todas as dependências em uma imagem.

**Dockerfile para .NET 8:**
```dockerfile
# Estágio 1: build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/EventFlow.API -c Release -o /app/publish

# Estágio 2: runtime (imagem menor, sem SDK)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Usuário sem privilégios (segurança)
RUN adduser --disabled-password --gecos "" appuser
USER appuser

EXPOSE 8080
ENTRYPOINT ["dotnet", "EventFlow.API.dll"]
```

**Por que multi-stage build?**
- Estágio de build usa a imagem SDK (~800MB)
- Estágio final usa só o runtime (~200MB)
- Imagem menor = deploy mais rápido + menos superfície de ataque

**Comandos básicos:**
```bash
docker build -t eventflow-api .
docker run -p 8080:8080 eventflow-api

# Com variáveis de ambiente (nunca coloque secrets no Dockerfile!)
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=db;..." \
  -e JwtSettings__Secret="seu-secret" \
  eventflow-api
```

---

### 2.3 Docker Compose (múltiplos containers)

Para rodar API + banco + outros serviços juntos:

```yaml
# docker-compose.prod.yml (simplificado)
services:
  api:
    build: .
    ports:
      - "80:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    env_file:
      - .env.production  # secrets separados do código!
    depends_on:
      db:
        condition: service_healthy

  db:
    image: postgres:16-alpine
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres_data:
```

---

## 3. Onde hospedar

### Opções em nuvem para projetos .NET

| Plataforma | Tipo | Gratuito | Melhor para |
|---|---|---|---|
| **Railway** | PaaS | Sim (limitado) | Projetos de estudo, MVP |
| **Render** | PaaS | Sim (limitado) | Projetos pessoais |
| **Fly.io** | Containers | Sim (limitado) | APIs + workers |
| **Azure App Service** | PaaS | Trial | .NET (Microsoft) |
| **AWS EC2/ECS** | IaaS/Containers | Trial | Escala |
| **DigitalOcean Droplet** | VPS | Pago | Controle total |

**Recomendação para estudo:** Railway ou Render — deploy com um clique a partir do GitHub.

---

## 4. Variáveis de Ambiente e Secrets

**REGRA DE OURO: Nunca commitar secrets no código.**

```
❌ appsettings.json:
{
  "ConnectionStrings": { "DefaultConnection": "Host=prod-db;Password=minhasenha" }
}

✅ Variáveis de ambiente no servidor:
ConnectionStrings__DefaultConnection=Host=prod-db;Password=minhasenha
JwtSettings__Secret=um-secret-longo-e-aleatorio
```

**No .NET, variáveis de ambiente com __ substituem : da hierarquia:**
```
JwtSettings__Secret → appsettings equivale a:
{
  "JwtSettings": { "Secret": "valor" }
}
```

**Como gerar um JWT secret seguro:**
```bash
# 256 bits = 32 bytes = bom para HS256
openssl rand -base64 32
# ou via dotnet:
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

---

## 5. Migrations em Produção

**NUNCA usar `MigrateAsync()` automático em produção** (como temos em Development no EventFlow).

```csharp
// ❌ Problema em produção:
if (app.Environment.IsDevelopment())
    await db.Database.MigrateAsync();  // ok em dev, mas...

// ✅ Em produção: rodar migration como parte do deploy
// Separado do startup da aplicação
```

**Abordagem correta:**
```bash
# Gerar script SQL da migration (revisar antes de aplicar)
dotnet ef migrations script --idempotent -o migration.sql

# Aplicar no banco de produção
psql -h prod-db -U postgres -d eventflow -f migration.sql

# Ou aplicar via EF (com conexão direta ao banco de prod)
dotnet ef database update --connection "Host=prod-db;..."
```

**Por que separar?**
- Se a migration falhar, o app não sobe (problema crítico em produção)
- Permite revisar o SQL antes de executar
- Múltiplas instâncias do app não tentam migrar ao mesmo tempo

---

## 6. CI/CD — Integração e Deploy Contínuos

CI/CD automatiza o caminho do `git push` até a produção.

```
git push → CI (testa) → CD (deploya se passou)
```

### GitHub Actions (o mais usado)

```yaml
# .github/workflows/deploy.yml
name: Build, Test e Deploy

on:
  push:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet test

  deploy:
    needs: test          # só roda se os testes passaram
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Build Docker image
        run: docker build -t eventflow-api .
      - name: Deploy para Railway
        run: railway up
        env:
          RAILWAY_TOKEN: ${{ secrets.RAILWAY_TOKEN }}
```

**Secrets no GitHub Actions:**
`Settings → Secrets and variables → Actions → New repository secret`
Nunca colocar secrets diretamente no YAML — usar `${{ secrets.NOME }}`.

---

## 7. Health Checks

O EventFlow já tem `/health`. Em produção isso é essencial:

```bash
# Docker verifica se o app está saudável
HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1
```

**Plataformas de hospedagem usam health check para:**
- Reiniciar containers que travaram
- Não enviar tráfego para instâncias doentes (load balancer)
- Alertar quando o banco está fora do ar

---

## 8. O que Estudar a Seguir

| Tópico | Por que estudar | Recurso sugerido |
|---|---|---|
| Docker avançado (volumes, networks) | Projetos multi-serviço | Docker Docs oficial |
| Kubernetes | Orquestração de containers em escala | k3s (versão leve para aprender) |
| GitHub Actions avançado | CI/CD completo com matrix tests | GitHub Actions docs |
| Azure DevOps | CI/CD no ecossistema Microsoft | Microsoft Learn |
| Terraform | Infrastructure as Code | HashiCorp tutorials |
| Observabilidade (Grafana, Prometheus) | Monitorar app em produção | Grafana Labs docs |

---

## 9. O que Evitar

| Prática ruim | Por quê | O que fazer |
|---|---|---|
| Secrets no `appsettings.json` commitado | Exposição de credenciais | Variáveis de ambiente / secrets manager |
| `MigrateAsync()` automático em produção | Falha crítica no startup | Migration separada do deploy |
| Imagem Docker com usuário root | Vulnerabilidade de segurança | Usar `USER appuser` não-root |
| Deploy direto na branch `main` sem testes | Quebra produção | Gate de testes no CI |
| String de conexão hardcoded no código | Impossível mudar sem recompilar | Configuração por ambiente |
| Logs com dados sensíveis (senhas, tokens) | Vazamento de dados | Nunca logar dados de auth |
| Container sem limites de CPU/memória | Um serviço mata todos os outros | `resources.limits` no compose/k8s |
