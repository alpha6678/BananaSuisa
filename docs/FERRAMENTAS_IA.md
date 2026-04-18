# Ferramentas de IA e MCPs

Como usar **Model Context Protocol (MCP)** no Cursor durante o desenvolvimento do monorepo **Ribanense Soluções**, e limites práticos dessas ferramentas frente a um app desktop WPF.

## O que é MCP no Cursor

Servidores MCP expõem ferramentas (navegador, GitHub, base de dados, etc.) que o agente pode invocar com parâmetros estruturados. Configuração:

| Escopo | Caminho | Uso |
|--------|---------|-----|
| Projeto | [`.cursor/mcp.json`](../.cursor/mcp.json) (se existir) | Partilhável com o time, versionado, sem segredos. |
| Usuário | `%USERPROFILE%\.cursor\mcp.json` | Tokens e servidores privados. |

Após alterar `mcp.json`, **reinicie o Cursor** para carregar servidores novos.

### Segurança

- Não commitar API keys, PATs ou passwords. Usar variáveis de ambiente ou arquivos ignorados pelo Git.
- Revisar permissões antes de ativar um MCP em repositórios com dados sensíveis.

## Servidores MCP recomendados

### Navegador integrado do Cursor (`cursor-ide-browser`)

Útil para testar páginas web e documentação externa (ex.: release notes do `winget-cli`, docs do `gh`). **Não** vê nem controla a janela WPF do Launcher ou dos apps.

### GitHub MCP (opcional)

Útil para issues e PRs sem sair do IDE. Requer PAT configurado. Pacote de referência: `@modelcontextprotocol/server-github`.

### Playwright MCP (quando houver UI web)

Só faz sentido quando o produto expuser algo web (painel de admin, site, etc.). Hoje, o produto é 100% desktop WPF; o Playwright MCP não alcança a UI.

### O que normalmente **não** é prioridade

- **Dart MCP**: stack Flutter/Dart; fora do escopo.
- **MCPs de base de dados**: só entrariam se algum app passar a depender de BD exposta ao agente.

## Testes fora de MCP

MCP complementa o desenvolvimento; não substitui testes no Windows:

- Testes unitários `.NET` via `dotnet test` (cobrem `PluginSDK`, `Launcher`, e cada app).
- Testes manuais elevados (UAC) para fluxos de `winget`, UWP, drivers.
- Smoke test end-to-end: `rb.cmd release` em modo dry-run, instalar via Launcher, validar update.

## Fluxo sugerido para o agente

1. Ler o schema das ferramentas MCP antes de invocar.
2. Para UI WPF: não assumir que um browser MCP validou; indicar no resumo se falta teste manual.
3. Alterações em `PluginSDK` quebram contrato: incrementar major SemVer e avisar os apps afetados.

## Referências rápidas

- [Model Context Protocol — especificação](https://modelcontextprotocol.io/)
- [GitHub CLI](https://cli.github.com/)
- [WPF-UI](https://github.com/lepoco/wpfui)
- [LiteDB](https://www.litedb.org/)

## Resolução de problemas

| Sintoma | Ação |
|---------|------|
| Servidor MCP não aparece | Reiniciar Cursor; validar JSON de `.cursor/mcp.json`. |
| `npx` não encontrado (se usar Playwright MCP) | Instalar Node.js LTS e reabrir terminal. |
| Agente não vê janela do Launcher | Esperado: WPF não é acessível via browser MCP. Validar manualmente. |
