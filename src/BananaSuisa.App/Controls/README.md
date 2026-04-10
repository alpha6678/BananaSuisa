# Componentes Visuais do BananaSuisa.App

Este diretório contém os componentes reutilizáveis (Widgets) e as partes estruturais da interface WPF.

## Estrutura do Layout (Shell)

A janela principal (`MainWindow.xaml`) atua como um *Shell* (Casca) que hospeda a aplicação usando três áreas principais:
1. **SidebarMenu**: Menu lateral de navegação (fixo à esquerda).
2. **MainContentArea** (`x:Name="MainContentArea"`): Um `ContentControl` central onde as *Views* (como a `DashboardView`) são injetadas dependendo do que o usuário clica no menu. O nome é usado por comportamentos de scroll (ver `Behaviors/README.md`).
3. **StatusFooter**: Barra de rodapé para mostrar progresso, mensagens rápidas e versão do app.
4. **LoadingOverlay**: Camada com fundo semi-transparente que pode cobrir a interface para indicar que uma operação em segundo plano está acontecendo (ex: instalação do Winget).

No fluxo **Instalar**, a faixa **Log de instalação** (abaixo da área central) usa `TextBoxWheelBehavior` para que a roda do rato possa mover o scroll da view principal quando o próprio log não precisa de scroll interno.

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

---

## Comportamentos de scroll (pasta `Behaviors/`)

Não são controlos visuais, mas propriedades anexadas para a roda do rato e `ScrollViewer`. Documentação: [`../Behaviors/README.md`](../Behaviors/README.md).

### `LogViewerWidget` e scroll aninhado

Se este widget for colocado **dentro** de outro `ScrollViewer` (página já rolável), o scroll interno do widget pode interferir na roda; nesse caso avalie o mesmo padrão de encaminhamento descrito em `Behaviors/README.md` ou evite duplo scroll na mesma direção.
