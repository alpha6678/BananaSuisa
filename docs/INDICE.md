# Indice de documentacao

Este e o ponto de entrada para a documentacao do BananaSuisa. Use este arquivo como mapa rapido antes de abrir documentos mais especificos.

## Comece aqui

| Documento | Publico principal | Quando ler |
|-----------|-------------------|------------|
| [`../README.md`](../README.md) | Todos | Para entender a estrutura do projeto, build e pastas principais. |
| [`../CONTRIBUTING.md`](../CONTRIBUTING.md) | Contribuidores | Antes de editar codigo, recursos ou scripts. |
| [`AMBIENTE.md`](AMBIENTE.md) | Contribuidores | Para configurar maquina, ferramentas e pre-requisitos. |

## Operacao e manutencao

| Documento | Foco |
|-----------|------|
| [`FERRAMENTAS_CLI.md`](FERRAMENTAS_CLI.md) | CLI do projeto, comandos locais, ferramentas externas e CI. |
| [`FERRAMENTAS_IA.md`](FERRAMENTAS_IA.md) | MCPs, Playwright, browser integrado e limites de teste no app atual. |
| [`REFERENCIAS_EXTERNAS.md`](REFERENCIAS_EXTERNAS.md) | Links oficiais, bibliotecas, ferramentas e documentacao canonica. |
| [`MELHORIAS.md`](MELHORIAS.md) | Changelog resumido e correcoes estruturais recentes. |

## Agentes e decisoes

| Documento | Foco |
|-----------|------|
| [`../AGENTS.md`](../AGENTS.md) | Regras e contexto para agentes de IA no repositorio. |
| [`adr/README.md`](adr/README.md) | Guia da pasta de ADRs e padrao de decisoes arquiteturais. |
| [`adr/ADR-000-formato.md`](adr/ADR-000-formato.md) | ADR inicial que define o formato adotado. |
| [`adr/ADR-001-stack-ui.md`](adr/ADR-001-stack-ui.md) | Decisao da stack inicial da nova UI (.NET 10 + WPF). |

## Migracao para .NET

| Documento | Foco |
|-----------|------|
| [`ROADMAP_MIGRACAO.md`](ROADMAP_MIGRACAO.md) | Fases, criterios de saida, riscos e direcao da migracao. |
| [`MAPEAMENTO_PS1_PARA_DOTNET.md`](MAPEAMENTO_PS1_PARA_DOTNET.md) | Inventario dos 9 modulos atuais e destino sugerido na solution futura. |

## Dominio do produto

| Documento | Foco |
|-----------|------|
| [`UI_SHELL.md`](UI_SHELL.md) | Estrutura partilhada de layout: margens, áreas de log, botões, e controlos base de pesquisa. |
| [`UI_INSTALAR.md`](UI_INSTALAR.md) | Vista Instalar: pesquisa, grelhas, botões, fila/cores; rodapé da janela só com log. |
| [`../BananaSuisa_desenvolvimento/docs/ARQUITETURA.md`](../BananaSuisa_desenvolvimento/docs/ARQUITETURA.md) | Ordem de carga, dados e build consolidado. |
| [`../BananaSuisa_desenvolvimento/docs/FLUXO_INSTALACAO.md`](../BananaSuisa_desenvolvimento/docs/FLUXO_INSTALACAO.md) | Fluxo de instalacao e atualizacao via `winget`. |
| [`../BananaSuisa_desenvolvimento/docs/SOLUCAO_PROBLEMAS.md`](../BananaSuisa_desenvolvimento/docs/SOLUCAO_PROBLEMAS.md) | Diagnostico rapido de problemas de execucao. |

## Especialistas tecnicos

Os documentos abaixo ficam em `BananaSuisa_desenvolvimento/especialistas/` e guardam conhecimento de temas especificos:

| Documento | Tema |
|-----------|------|
| [`../BananaSuisa_desenvolvimento/especialistas/uwp_appinstaller.md`](../BananaSuisa_desenvolvimento/especialistas/uwp_appinstaller.md) | App Installer, AppX e cenarios de reparo do ecossistema `winget`. |
| [`../BananaSuisa_desenvolvimento/especialistas/winget_exit_codes.md`](../BananaSuisa_desenvolvimento/especialistas/winget_exit_codes.md) | Codigos de saida do `winget` e referencia upstream. |
| [`../BananaSuisa_desenvolvimento/especialistas/powershell_ui.md`](../BananaSuisa_desenvolvimento/especialistas/powershell_ui.md) | Responsividade e limites da UI PowerShell/WinForms atual. |

## Convencoes desta documentacao

- Preferir nomes em letras maiusculas para documentos de processo e referencia.
- Registrar conhecimento especifico de dominio em `especialistas/`.
- Registrar decisoes de arquitetura em `docs/adr/`.
- Atualizar a documentacao junto com a mudanca de codigo, sempre que o comportamento ou os pre-requisitos mudarem.

## Proximos documentos previstos

- decisoes futuras em `docs/adr/ADR-001-*`
- detalhamento da stack UI escolhida
- estrategia final de empacotamento `.exe` e `.msi`
