using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;

namespace Ribanense.Solucoes.App.Recuperacao.Converters;

/// <summary>Severidade de um achado → cor do marcador.</summary>
public sealed class CheckSeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is CheckSeverity s ? BrushFor(s) : Brushes.Gray;

    private static SolidColorBrush BrushFor(CheckSeverity severity) => severity switch
    {
        CheckSeverity.Ok => new SolidColorBrush(Color.FromRgb(0x27, 0x86, 0x4E)),
        CheckSeverity.Warning => new SolidColorBrush(Color.FromRgb(0xB5, 0x79, 0x1A)),
        CheckSeverity.Error => new SolidColorBrush(Color.FromRgb(0xC2, 0x3B, 0x22)),
        _ => new SolidColorBrush(Color.FromRgb(0x6B, 0x6B, 0x6B)),
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Estado de um passo do reparo → cor do marcador.</summary>
public sealed class PassoEstadoToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ReparoEstado e ? BrushFor(e) : Brushes.Gray;

    private static SolidColorBrush BrushFor(ReparoEstado estado) => estado switch
    {
        ReparoEstado.Ok => new SolidColorBrush(Color.FromRgb(0x27, 0x86, 0x4E)),
        ReparoEstado.Aviso => new SolidColorBrush(Color.FromRgb(0xB5, 0x79, 0x1A)),
        ReparoEstado.Falhou => new SolidColorBrush(Color.FromRgb(0xC2, 0x3B, 0x22)),
        ReparoEstado.Executando => new SolidColorBrush(Color.FromRgb(0x2E, 0x6F, 0xDB)),
        _ => new SolidColorBrush(Color.FromRgb(0x6B, 0x6B, 0x6B)),
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Referência nula (ou string vazia) → Collapsed (usado em dicas opcionais).</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null || (value is string s && s.Length == 0) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
