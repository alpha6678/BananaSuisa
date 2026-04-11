# Componentes Visuais do BananaSuisa.App

Este diretório contém os componentes reutilizáveis (Widgets) e as partes estruturais da interface WPF.

## Estrutura do Layout (Shell)

A janela principal (`MainWindow.xaml`) atua como um *Shell* (Casca) que hospeda a aplicação usando três áreas principais:
1. **SidebarMenu**: Menu lateral de navegação (fixo à esquerda).
2. **MainContentArea** (`x:Name="MainContentArea"`): Um `ContentControl` central onde as *Views* (como a `DashboardView`) são injetadas dependendo do que o usuário clica no menu. O nome é usado por comportamentos de scroll (ver `Behaviors/README.md`).
3. **StatusFooter**: Barra de rodapé para mostrar progresso, mensagens rápidas e versão do app.
4. **LoadingOverlay**: Camada com fundo semi-transparente que pode cobrir a interface para indicar que uma operação em segundo plano está acontecendo (ex: instalação do Winget).

No fluxo **Instalar**, as ações **Cancelar** / **Instalar** estão na **InstallRunView**, por baixo das grelhas. O **rodapé** da janela (`MainWindow`) mostra só uma **linha de log** compacta (`TextBlock`, truncagem e tooltip), sem título.

---

## Componentes Disponíveis

Para usar qualquer um destes componentes, inclua o namespace no cabeçalho do seu XAML:
`xmlns:controls="clr-namespace:BananaSuisa.App.Controls"`

### 1. `InfoCardWidget`
Um card padrão com bordas arredondadas e as cores corretas do tema do aplicativo, projetado para exibir blocos de informação de forma isolada.

**Propriedades Expansíveis:**
- `CardTitle` (string): O título em destaque na parte superior do card.
- `CardContent` (object/UIElement): O conteúdo principal do card. Pode ser texto puro ou outro controle WPF.

**Exemplo de Uso:**
```xml
<controls:InfoCardWidget CardTitle="Título do Card">
    <controls:InfoCardWidget.CardContent>
        <TextBlock Text="Conteúdo do card aqui..." />
    </controls:InfoCardWidget.CardContent>
</controls:InfoCardWidget>
```

### 2. `LoadingOverlay`
Uma tela de bloqueio com um spinner de progresso (`ProgressBar` indeterminado) para indicar carregamento. Você pode ligar e desligar esse overlay a partir da ViewModel ligando o `Visibility` a uma propriedade booleana.

**Propriedades Expansíveis:**
- `Message` (string): A mensagem exibida embaixo do spinner. O padrão é "Carregando...".

**Exemplo de Uso:**
```xml
<!-- Ele normalmente é fixado no topo do Grid principal cobrindo os outros controles -->
<controls:LoadingOverlay Message="Buscando atualizações..." 
                         Visibility="{Binding IsLoading, Converter={StaticResource BooleanToVisibilityConverter}}" />
```

### 3. `LogViewerWidget`
Um painel rolável desenhado especificamente para mostrar fluxo de texto estilo terminal ou output de logs em tempo real. Ele rola automaticamente para o final conforme novos itens são adicionados à coleção conectada.

**Propriedades Expansíveis:**
- `Logs` (IEnumerable): A lista (preferencialmente `ObservableCollection<string>`) de textos de log.

**Exemplo de Uso:**
```xml
<controls:LogViewerWidget Logs="{Binding MeuLogCollection}" Height="200" />
```

### 4. `StatusFooter`
A barra na base do aplicativo. Ideal para o usuário entender se a aplicação está parada ou processando algo em background.

**Propriedades Expansíveis:**
- `StatusText` (string): Mensagem de estado do lado esquerdo (ex: "Pronto" ou "Baixando dependências").
- `ProgressValue` (double): Valor da barra de progresso (0 a 100).
- `IsProgressVisible` (bool): Liga/desliga a visualização da barra de progresso horizontal no meio.
- `RightText` (string): Texto no canto direito da barra (normalmente usado para a versão da aplicação).

**Exemplo de Uso:**
```xml
<controls:StatusFooter StatusText="Processando..." ProgressValue="45" IsProgressVisible="True" RightText="v1.0" />
```

### 5. `SidebarMenu`
A barra de navegação principal. Possui botões estilizados. Por padrão eles ativam o comando `NavigateCommand` associado ao ViewModel atual para injetar uma nova View no centro da tela.

### 6. `CompactActivityLogStrip`
Rodapé de log desenhado para conter mensagens breves de atividade. Ocupa o mínimo de espaço vertical, limitando o texto a 2 linhas com reticências (`...`) e permitindo ver o log completo no ToolTip.

**Exemplo de Uso:**
```xml
<controls:CompactActivityLogStrip LogText="{Binding ActivityLog}" />
```

### 7. `SearchTextButtonRow`
Um campo de busca associado a um botão de pesquisa, otimizado para o tema da app (com hint textual e atalho de `Enter`).

**Exemplo de Uso:**
```xml
<controls:SearchTextButtonRow SearchText="{Binding Query}" 
                              SearchCommand="{Binding SearchCommand}" 
                              SearchButtonText="Procurar" 
                              WatermarkText="Digite o termo..." />
```

### 8. `ActionButtonStrip`
Um conjunto de 1 a 2 botões padrão alinhados à direita, utilizado tipicamente no fim de uma view.

**Exemplo de Uso:**
```xml
<controls:ActionButtonStrip PrimaryButtonText="Guardar" 
                            PrimaryButtonCommand="{Binding SaveCommand}" 
                            SecondaryButtonText="Cancelar" 
                            SecondaryButtonCommand="{Binding CancelCommand}" />
```

### 9. `InstallRetrySummaryDialog`
Overlay-dialog de resumo exibido ao final de um lote de instalação. Mostra totais de sucesso/falha, uma grade de candidatos de retry por similaridade (com checkbox de aprovação) e botões para **Fechar**, **Tentar todos sugeridos** ou **Revisar um por um**.

Controlado pela propriedade `ShowRetrySummary` do ViewModel. Os candidatos vêm de `RetryCandidates` (coleção de `RetryCandidateViewModel`).

**Exemplo de Uso (já integrado no `MainWindow.xaml`):**
```xml
<controls:InstallRetrySummaryDialog Grid.RowSpan="2"
                                     Visibility="{Binding ShowRetrySummary, Converter={StaticResource BooleanToVisibilityConverter}}" />
```

---

## Comportamentos de scroll (pasta `Behaviors/`)

Não são controlos visuais, mas propriedades anexadas para a roda do rato e `ScrollViewer`. Documentação: [`../Behaviors/README.md`](../Behaviors/README.md).

### `LogViewerWidget` e scroll aninhado

Se este widget for colocado **dentro** de outro `ScrollViewer` (página já rolável), o scroll interno do widget pode interferir na roda; nesse caso avalie o mesmo padrão de encaminhamento descrito em `Behaviors/README.md` ou evite duplo scroll na mesma direção.
