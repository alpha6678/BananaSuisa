namespace Ribanense.Solucoes.App.Winget.Services;

public interface IWingetLocator
{
    /// <summary>Caminho de <c>winget.exe</c> ou <c>null</c> se não encontrado.</summary>
    string? TryLocate();
}
