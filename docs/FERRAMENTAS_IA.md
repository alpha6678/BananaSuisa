# Ferramentas de IA, MCPs e testes

Este guia alinha o uso de **Model Context Protocol (MCP)** no Cursor, limitações para o BananaSuisa atual (WinForms / PowerShell desktop) e **testes** complementares que o MCP não substitui.

Para **terminal e scripts do repositório** (`bs.cmd`, build, CI), veja [FERRAMENTAS_CLI.md](FERRAMENTAS_CLI.md).

## O que é MCP no Cursor

Servidores MCP expõem ferramentas (navegador, GitHub, base de dados, etc.) que o agente pode invocar com parâmetros estruturados. A configuração pode ser:

| Escopo | Caminho típico (Windows) | Uso |
|--------|---------------------------|-----|
| **Projeto** | [`.cursor/mcp.json`](../.cursor/mcp.json) na raiz do repositório aberto no Cursor | Partilhável com a equipa, versionado (sem segredos). |
| **Utilizador** | `%USERPROFILE%\.cursor\mcp.json` | Máquina pessoal, tokens e servidores privados. |

Após alterar `mcp.json`, é necessário **reiniciar completamente o Cursor** para carregar servidores novos ou modificados.

### Segurança

- Não commitar API keys, PATs ou passwords. Use variáveis de ambiente referenciadas na configuração do Cursor ou ficheiros locais ignorados pelo Git.
- Revise permissões de cada servidor MCP antes de ativar em repositórios com dados sensíveis.

---

## Servidores MCP recomendados

### 1. Navegador integrado do Cursor (`cursor-ide-browser`)

Fornecido pelo próprio Cursor quando ativo nas definições (**Cursor Settings → MCP / Tools**). Útil para:

- Testar **interfaces web** (futuras apps Blazor/WebView2 hospedadas em `localhost`, sites de documentação, páginas de login internas).
- Inspeção por snapshot (árvore acessível), cliques, preenchimento de formulários, screenshots.

**Limitação para o BananaSuisa hoje:** a UI principal é **Windows Forms** num processo desktop nativo. O MCP de browser **não** vê nem controla essa janela. Para o fluxo atual, continue a validar com execução manual ou automação desktop (secção abaixo).

### 2. Playwright MCP (`@playwright/mcp`)

Automação de browser orientada a testes e2e e fluxos reproduzíveis. Documentação oficial: [Playwright MCP](https://playwright.dev/docs/test-agents#mcp-server).

**Requisitos:** Node.js 18 ou superior (`node` / `npx` no `PATH`).

O repositório inclui exemplo em [`.cursor/mcp.json`](../.cursor/mcp.json). Se preferir não usar Playwright no projeto, remova o bloco `playwright` do JSON e guarde apenas o que precisar.

**Quando usar no BananaSuisa:**

- Após migração para UI web ou quando existirem cenários testáveis num browser real.
- Regressão de fluxos que envolvam páginas externas (por exemplo, documentação WinGet) — com cuidado a flakiness e rede.

**Conflitos:** Ter dois servidores a controlar o mesmo browser pode confundir o agente. Prefira **um** fluxo principal (integrado Cursor **ou** Playwright) por tarefa.

### 3. GitHub MCP (opcional)

Útil para issues, PRs e pesquisa no repositório sem sair do IDE. Requer normalmente `GITHUB_PERSONAL_ACCESS_TOKEN` (ou OAuth conforme o pacote). **Não** versionize o token; configure em definições globais do Cursor ou variáveis de ambiente do sistema.

Pacote de referência (verificar nome atual no npm): `@modelcontextprotocol/server-github`.

### 4. O que normalmente **não** é prioridade para este projeto

- **Dart MCP:** orientado a ecossistema Flutter/Dart; não alinha com a stack atual nem com a migração .NET prevista.
- **Servidores de base de dados:** só relevantes se o produto passar a depender de BD exposta ao agente.

---

## Testes fora de MCP (obrigatório para qualidade real)

MCP complementa o desenvolvimento; **não substitui** testes no Windows com elevação e WinGet.

### Aplicação atual (PowerShell + WinForms)

1. **Manual estruturado:** checklist por ecrã (instalar, atualizar, remover, reparar, busca, catálogo) com utilizador administrador quando aplicável.
2. **Pester (PowerShell):** testes unitários e de integração leves para funções puras em módulos (`BananaSuisa_desenvolvimento/`), sem abrir formulário — reduz regressões em `bootstrap`, parsing e helpers. Instalação: módulo `Pester` da PowerShell Gallery.
3. **Build:** após alterações, executar [`ferramentas/Gerar_BananaSuisa.ps1`](../ferramentas/Gerar_BananaSuisa.ps1) e validar o `BananaSuisa.ps1` gerado.

### Futuro (.NET)

- **UI:** WinAppDriver, Appium ou fornecedores comerciais para WinUI/WPF, conforme ADR de stack.
- **Testes de integração WinGet:** preferir ambientes descartáveis ou VMs; evitar corrida em máquina de produção.

---

## Fluxo sugerido para o agente (Cursor)

1. Ler o schema das ferramentas MCP disponíveis antes de invocar (evita erros de parâmetros).
2. Para alterações na UI desktop atual: **não** assumir que o browser MCP validou o app — indicar no resumo que falta teste manual.
3. Para documentação ou apps web: usar snapshot + uma ação de cada vez; após navegação, novo snapshot antes do próximo clique.

---

## Referências rápidas

- [WinGet CLI (releases)](https://github.com/microsoft/winget-cli/releases)
- [Model Context Protocol — especificação](https://modelcontextprotocol.io/)
- [Playwright MCP (npm)](https://www.npmjs.com/package/@playwright/mcp)

---

## Resolução de problemas

| Sintoma | Ação |
|---------|------|
| Servidor MCP não aparece | Reiniciar Cursor por completo; confirmar JSON válido em `.cursor/mcp.json`. |
| `npx` não encontrado | Instalar Node.js LTS e reabrir terminal/Cursor. |
| Playwright falha ao arrancar | Executar uma vez `npx -y @playwright/mcp@latest` no terminal para ver erros; instalar browsers se o pacote pedir (`npx playwright install`). |
| Agente “congela” no browser | Reduzir passos; usar waits curtos e snapshots incrementais (ver instruções do servidor browser no Cursor). |
