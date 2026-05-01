using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ribanense.Solucoes.UI;

/// <summary>
/// Copia linhas de log para a área de transferência (padrão dos apps WPF do monorepo).
/// </summary>
public static class LogLinesClipboard
{
    /// <returns>true se havia texto e a cópida foi bem-sucedida.</returns>
    public static bool TryCopy(IEnumerable<string>? lines)
    {
        if (lines is null) return false;

        string text = string.Join(System.Environment.NewLine, lines);
        if (string.IsNullOrEmpty(text)) return false;

        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Copia as linhas; se falhar e houver conteúdo, exibe aviso modal.
    /// </summary>
    public static void CopyOrWarn(IEnumerable<string>? lines, string dialogTitle)
    {
        if (lines is null) return;

        int count = lines is ICollection<string> c ? c.Count : lines.Count();
        if (count == 0) return;

        if (TryCopy(lines)) return;

        MessageBox.Show(
            "Nao foi possivel copiar para a area de transferencia.",
            dialogTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
