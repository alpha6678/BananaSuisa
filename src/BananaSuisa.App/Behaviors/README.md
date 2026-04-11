# Comportamentos anexados (scroll / roda)

Comportamentos em `BananaSuisa.App.Behaviors` tratam limitações comuns do WPF em que a **roda do rato** fica “presa” em controlos com scroll próprio (`DataGrid`, `TextBox` multilinha) em vez de deslocar a página.

| Classe | Uso típico |
|--------|------------|
| `DataGridWheelBehavior` | Ligado via estilo `LogsDataGridStyle` em `Styles/Controls.xaml`: encaminha a roda para o `ScrollViewer` ancestral quando a grelha não precisa de scroll interno. |
| `TextBoxWheelBehavior` | Em `TextBox` multilinha: quando o texto não absorve a roda (pouco texto ou já no topo/fundo), rola o `ScrollViewer` da view em `MainContentArea`. Requer `x:Name="MainContentArea"` no `ContentControl`. (O log de instalação no fluxo Instalar é um `TextBlock` compacto e não usa este comportamento.) |

Evitar **dois `ScrollViewer` aninhados** na mesma direção para o mesmo conteúdo (ex.: shell com scroll + view filha com scroll); preferir um único scroll no shell ou na view raiz.
