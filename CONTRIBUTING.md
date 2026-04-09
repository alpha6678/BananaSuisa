# Contribuindo com o BananaSuisa

Este documento define o fluxo minimo para evoluir o BananaSuisa com seguranca enquanto o produto ainda e modular em PowerShell + WinForms e esta em preparacao para a migracao para .NET.

## Fluxo rapido

1. Trabalhe sempre nos modulos em `BananaSuisa_desenvolvimento/`, nunca no `BananaSuisa.ps1` gerado.
2. Gere o consolidado com `.\bs.cmd build`.
3. Teste o `BananaSuisa.ps1` como administrador quando a mudanca afetar `winget`, reparo, drivers, atualizacoes ou scripts do sistema.
4. Atualize a documentacao relacionada sempre que alterar comportamento, requisitos, comandos ou estrutura.

## Estrutura do codigo

| Local | Responsabilidade |
|-------|------------------|
| `BananaSuisa_desenvolvimento/nucleo/` | Bootstrap, requisitos, workspace, configuracao, logs e deteccao do `winget`. |
| `BananaSuisa_desenvolvimento/interface/` | Tema, layout e views da UI WinForms. |
| `BananaSuisa_desenvolvimento/funcionalidades/` | Busca, catalogo, downloads, acoes e integracoes do produto. |
| `BananaSuisa_desenvolvimento/eventos/` | Ligacao entre controles da UI e os fluxos funcionais. |
| `BananaSuisa_recursos/` | Configs, catalogos base e memoria de execucao (`BananaSuisa_memoria`). |
| `ferramentas/` | Build consolidado e CLI de desenvolvimento. |
| `src/` | Esqueleto .NET inicial (`App`, `Core`, `Services`, `Infrastructure`, `Shared`). |
| `BananaSuisa.slnx` | Solution da nova base .NET. |

## Ordem de carga atual

O consolidado e gerado por `ferramentas/Gerar_BananaSuisa.ps1` nesta ordem:

1. `nucleo/bootstrap.ps1`
2. `interface/theme.ps1`
3. `funcionalidades/search.ps1`
4. `interface/layout.ps1`
5. `funcionalidades/catalog.ps1`
6. `interface/views.ps1`
7. `funcionalidades/actions.ps1`
8. `eventos/app.events.ps1`

Se um novo modulo for criado, atualize a ordem no gerador e a documentacao arquitetural para evitar divergencia entre o ambiente modular e o `BananaSuisa.ps1`.

## Regras praticas

- Edite a versao somente em `BananaSuisa_desenvolvimento/nucleo/versao.ps1`.
- Nao altere manualmente o `BananaSuisa.ps1`; ele e artefato gerado e ignorado pelo Git.
- Coloque logica reutilizavel em funcoes dos modulos, e nao diretamente em handlers da UI.
- Preserve a separacao entre `interface/`, `funcionalidades/`, `eventos/` e `nucleo/`.
- Mudancas que afetem paths, memoria ou sincronizacao de dados devem considerar `BananaSuisa_recursos/BananaSuisa_memoria/`.
- Se tocar em `winget`, App Installer, drivers ou update do Windows, registe claramente o fluxo testado.

## Validacao minima antes de concluir uma mudanca

1. Executar `.\bs.cmd build`.
2. Confirmar que `BananaSuisa.ps1` foi gerado sem erros.
3. Se a mudanca tocar a solution .NET, executar `dotnet build .\BananaSuisa.slnx`.
4. Abrir a aplicacao impactada e validar ao menos o fluxo alterado.
5. Se a mudanca afetar instalacao, remocao, reparo ou catalogo, testar com privilegios elevados.
6. Revisar os documentos afetados e atualizar links, exemplos e pre-requisitos.

## Como reportar bugs ou regressao

Inclua sempre que possivel:

- Versao do Windows.
- Versao do PowerShell (`$PSVersionTable.PSVersion`).
- Se a execucao foi feita como administrador.
- Comando executado (`.\bs.cmd build`, `BananaSuisa.ps1`, etc.).
- Codigo de saida do `winget`, quando existir.
- Caminho do log: `BananaSuisa_recursos\BananaSuisa_memoria\Registros\BananaSuisa.json`.
- Passos para reproduzir e resultado esperado x obtido.

## Documentacao relacionada

- [`docs/INDICE.md`](docs/INDICE.md)
- [`docs/AMBIENTE.md`](docs/AMBIENTE.md)
- [`docs/FERRAMENTAS_CLI.md`](docs/FERRAMENTAS_CLI.md)
- [`docs/FERRAMENTAS_IA.md`](docs/FERRAMENTAS_IA.md)
- [`BananaSuisa_desenvolvimento/docs/ARQUITETURA.md`](BananaSuisa_desenvolvimento/docs/ARQUITETURA.md)
