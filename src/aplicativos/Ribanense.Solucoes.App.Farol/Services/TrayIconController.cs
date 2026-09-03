using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Ribanense.Solucoes.App.Farol.Services;

/// <summary>
/// Ícone de bandeja: o farol precisa ficar acordado para a malha existir, e a
/// bandeja é o único lugar honesto para sinalizar isso ao usuário.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly NotifyIcon _icon;
    private bool _disposed;

    public TrayIconController(Action onOpen, Action onCapture, Action onExit)
    {
        ArgumentNullException.ThrowIfNull(onOpen);
        ArgumentNullException.ThrowIfNull(onCapture);
        ArgumentNullException.ThrowIfNull(onExit);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir o Farol", null, (_, _) => onOpen());
        menu.Items.Add("Capturar dossiê agora", null, (_, _) => onCapture());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => onExit());

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Farol — Ribanense Soluções",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _icon.DoubleClick += (_, _) => onOpen();
    }

    public void Notify(string title, string message)
    {
        if (_disposed) return;

        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message;
        _icon.ShowBalloonTip(5000);
    }

    /// <summary>
    /// Usa o ícone já embutido no próprio executável (<c>ApplicationIcon</c>) em
    /// vez de um .ico solto ao lado dele: o arquivo solto não sobrevive à
    /// publicação em arquivo único e a bandeja acabaria com o ícone genérico do
    /// Windows justamente no app que vive lá.
    /// </summary>
    private static Icon LoadIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } exe && File.Exists(exe))
            {
                Icon? embedded = Icon.ExtractAssociatedIcon(exe);
                if (embedded is not null) return embedded;
            }
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _icon.Visible = false;
        _icon.Dispose();
    }
}
