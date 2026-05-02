using System.Collections;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace Ribanense.Solucoes.App.Chocolatey.Converters;

public sealed class LogLinesToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable lines)
        {
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                if (line != null)
                {
                    sb.AppendLine(line.ToString());
                }
            }
            return sb.ToString().TrimEnd();
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
