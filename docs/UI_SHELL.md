# UI Shell e Estrutura Reutilizável

Este documento descreve a estrutura partilhada (chrome) das vistas da aplicação e os controlos reutilizáveis. O objetivo é manter a consistência visual em relação a margens, botões, campos de busca e registo de atividade (logs) em toda a aplicação.

## Contrato de Layout

A interface base baseia-se num esquema que pode ser assim representado:

```mermaid
flowchart TB
  subgraph main [MainWindow coluna direita]
    content[Row0 ContentControl CurrentView]
    log[Row1 Rodapé de Log (CompactActivityLogStrip)]
  end
  subgraph view [View exemplo]
    search[SearchTextButtonRow]
    body[Grelhas / Cards / Conteúdo]
    actions[ActionButtonStrip]
  end
  content --> view
  actions -.-> log
```

### Regras de Ouro

| Zona | Regra |
|------|-------|
| **Margens da página** | O `Grid` raiz de cada view deve usar uma margem padronizada: `Margin="{StaticResource WorkspacePageMargin}"` (para views com log partilhado/ações) ou `Margin="{StaticResource WorkspaceContentMargin}"` para views simples. |
| **Busca** | Colocada no topo da view (`Row=0`), usando o controlo `SearchTextButtonRow` (ou equivalente), seguida de uma margem vertical de espaçamento. |
| **Conteúdo** | Ocupa a maior parte do ecrã (`Height="*"`). |
| **Botões de ação** | Localizam-se abaixo do conteúdo principal, alinhados à direita, na mesma linha horizontal. Usar o `ActionButtonStrip` ou um `StackPanel` similar com botões de tamanho consistente (`MinWidth="140"`). |
| **Área de Log** | Existe **uma única faixa** global no `MainWindow` gerida por `CompactActivityLogStrip`. Vistas filhas **não devem** replicar "mini-logs" para a mesma atividade, exceto no ecrã completo dedicado a logs (`LogsView`). |

## Controlos Reutilizáveis (`src/BananaSuisa.App/Controls/`)

- **`CompactActivityLogStrip`**: Rodapé de log desenhado para conter mensagens breves de atividade. Ocupa o mínimo de espaço vertical (~32px), limitando o texto a 2 linhas com reticências (`...`) e permitindo ver o log completo no ToolTip.
- **`SearchTextButtonRow`**: Um campo de busca associado a um botão de pesquisa, otimizado para o tema da app (com "hint" textual e atalho de `Enter`).
- **`ActionButtonStrip`**: Um slot ou conjunto de botões padrão alinhados à direita.

Para detalhes práticos sobre como instanciar estes controlos, consulte o `README.md` dentro de `src/BananaSuisa.App/Controls/`.
