# Guia de Git e GitHub — Trabalho em Equipe com Controle de Versão

Git é a ferramenta mais usada no desenvolvimento de software. Dominar o básico resolve 95% dos problemas do dia a dia. Este guia cobre o que usar, como usar e o que evitar.

---

## 1. Os Conceitos Essenciais

### O que o Git rastreia

```
Working Directory → Staging Area → Repository (local) → Remote (GitHub)
    (seus arquivos)    (git add)      (git commit)        (git push)
```

**3 estados de um arquivo:**
- **Modified**: você alterou mas não adicionou ao stage
- **Staged**: marcado para entrar no próximo commit
- **Committed**: salvo permanentemente no histórico local

### Comandos que você usa todo dia

```bash
git status              # ver o estado atual — use sempre
git diff                # ver o que mudou (antes do stage)
git diff --staged       # ver o que está no stage

git add arquivo.cs      # adicionar arquivo específico ao stage
git add src/            # adicionar pasta inteira
git add -p              # adicionar por partes (interativo — recomendado!)

git commit -m "feat: adicionar BookTicketCommand"
git log --oneline       # histórico resumido
git log --oneline --graph --all  # histórico visual com branches
```

---

## 2. Branches — Trabalho em Paralelo

**Branch = linha de desenvolvimento independente.**

```bash
# Criar e trocar para nova branch
git checkout -b feature/book-ticket

# Listar branches
git branch -a

# Trocar de branch
git checkout main

# Deletar branch local (após merge)
git branch -d feature/book-ticket
```

### Estratégia de branches (Git Flow simplificado)

```
main          ← código estável, vai para produção
  └── develop ← integração de features (opcional)
        ├── feature/book-ticket     ← nova feature
        ├── fix/cancel-ticket-bug   ← correção de bug
        └── chore/update-packages   ← manutenção
```

**Nomenclatura de branches:**
```
feature/nome-da-feature   → nova funcionalidade
fix/descricao-do-bug       → correção de bug
chore/o-que-foi-feito      → manutenção (atualizar pacotes, refactor)
docs/o-que-foi-documentado → documentação
test/o-que-foi-testado     → adicionar/melhorar testes
```

---

## 3. Conventional Commits — Padrão de Mensagens

Convenção amplamente adotada: `tipo(escopo): descrição`.

```bash
# Tipos principais:
feat:     nova funcionalidade
fix:      correção de bug
docs:     apenas documentação
test:     adicionar/corrigir testes
refactor: refatoração (sem nova feature, sem bug fix)
chore:    tarefas de manutenção (atualizar deps, config)
perf:     melhoria de performance

# Exemplos reais:
git commit -m "feat(tickets): adicionar BookTicketCommand com retry de concorrência"
git commit -m "fix(auth): corrigir vazamento de informação no login"
git commit -m "test(tickets): adicionar teste de concorrência com TestContainers"
git commit -m "docs: adicionar guia de segurança"
git commit -m "refactor(events): extrair validação de capacidade para método privado"
```

**Por que isso importa?**
- `git log` fica legível sem precisar abrir cada commit
- Gera CHANGELOG automaticamente com ferramentas como `semantic-release`
- Facilita review de código em PRs

---

## 4. Pull Requests (PR) / Merge Requests (MR)

**Pull Request = proposta de merge de uma branch para outra.**

### Fluxo típico

```bash
# 1. Criar branch a partir de main
git checkout main
git pull origin main
git checkout -b feature/confirm-ticket

# 2. Desenvolver e commitar
# ... trabalho ...
git add .
git commit -m "feat(tickets): adicionar ConfirmTicketCommand"

# 3. Enviar para o GitHub
git push -u origin feature/confirm-ticket

# 4. Abrir PR no GitHub
# 5. Code review
# 6. Merge após aprovação
# 7. Deletar a branch
```

### O que colocar na descrição do PR

```markdown
## O que foi feito
- Adiciona endpoint POST /api/tickets/{id}/confirm
- Transição Reserved → Confirmed na máquina de estados do Ticket
- Gera TicketCode para uso futuro no QR Code (Fase 5)

## Como testar
1. Registrar como Attendee
2. Reservar ingresso em evento Published
3. POST /api/tickets/{id}/confirm
4. Verificar que status mudou para Confirmed e ticketCode foi gerado

## Checklist
- [x] Testes unitários adicionados
- [x] Build passando
- [x] Documentação atualizada
```

---

## 5. Merge vs Rebase

**Merge:** cria um commit de merge, preserva histórico completo.
**Rebase:** reescreve os commits em cima de outra branch, histórico linear.

```bash
# Merge (mais seguro, mais comum em equipe)
git checkout main
git merge feature/book-ticket

# Rebase (histórico mais limpo, mas reescreve commits)
git checkout feature/book-ticket
git rebase main
```

**Regra de ouro:** Nunca rebase branches que já foram pushadas e outras pessoas estão usando.

**Para projetos pessoais/estudo:** rebase é ótimo para manter histórico limpo.
**Para equipes:** merge é mais seguro.

---

## 6. Desfazer Erros Comuns

```bash
# Desfazer o último commit (mantém as mudanças nos arquivos)
git reset HEAD~1

# Desfazer staged (tirar do stage, manter no working directory)
git restore --staged arquivo.cs

# Descartar mudanças NÃO commitadas (CUIDADO: irreversível)
git restore arquivo.cs

# Corrigir a mensagem do último commit (antes de fazer push)
git commit --amend -m "nova mensagem"

# Ver o que foi deletado / encontrar commits perdidos
git reflog
```

---

## 7. .gitignore — O que Não Versionar

O projeto já tem um `.gitignore` configurado. Itens críticos:

```gitignore
# Secrets — NUNCA versionar
appsettings.Development.json
*.env
.env.local
secrets.json

# Artefatos de build
bin/
obj/
*.dll

# Logs e dados locais
logs/
*.log

# IDE
.vs/
.vscode/
*.user
```

**Verificar se um arquivo está sendo ignorado:**
```bash
git check-ignore -v arquivo.cs
```

---

## 8. GitHub — Funcionalidades Importantes

### Issues
Rastreamento de tarefas, bugs e melhorias.
```
Good practice:
- Descrever o problema claramente
- Adicionar labels (bug, enhancement, documentation)
- Linkar no commit: "fix: corrigir validação de capacidade. Closes #42"
```

### GitHub Actions
CI/CD diretamente no repositório. Ver `docs/guias/deploy.md`.

### GitHub Pages
Hospedagem gratuita para sites estáticos (documentação, portfolio).

### Proteção de Branch
Em `Settings → Branches → Add rule`:
- Require pull request before merging
- Require status checks (testes passando antes do merge)
- Require linear history

---

## 9. Boas Práticas

```bash
# ✅ Commit cedo, commit frequente (mas com significado)
# Não acumule 3 horas de trabalho em um commit

# ✅ Um commit = uma mudança lógica
# Se você está corrigindo um bug E adicionando uma feature, faça 2 commits

# ✅ Pull antes de push
git pull --rebase origin main
git push origin main

# ✅ Revisão antes de commitar
git diff --staged   # revisar o que vai entrar
git add -p          # adicionar por hunks (pedaços)
```

---

## 10. O que Estudar a Seguir

| Tópico | Por que estudar |
|---|---|
| `git bisect` | Encontrar qual commit introduziu um bug |
| `git cherry-pick` | Aplicar commit específico em outra branch |
| `git stash` | Salvar trabalho temporariamente |
| GitHub CLI (`gh`) | Criar PRs, issues pela linha de comando |
| Semantic Versioning (semver) | Versionar releases: MAJOR.MINOR.PATCH |
| Git hooks | Rodar testes/lint antes de commitar |

---

## 11. O que Evitar

| Prática ruim | Consequência | O que fazer |
|---|---|---|
| `git add .` sempre | Commita arquivos desnecessários/secrets | `git add -p` ou por arquivo |
| `git push --force` na main | Apaga o histórico de outros | Nunca force push na main |
| Commits gigantes com tudo | Impossível fazer review / reverter parte | Commits pequenos e focados |
| Mensagens como "fix" ou "update" | Histórico ilegível | Conventional commits |
| Commitar appsettings.Development.json | Expõe secrets | .gitignore + user-secrets |
| Branch de feature com meses de vida | Merge conflicts enormes | PRs pequenas e frequentes |
| Resolver conflito sem entender | Pode apagar código de outra pessoa | Comunicar com o autor, entender o conflito |
