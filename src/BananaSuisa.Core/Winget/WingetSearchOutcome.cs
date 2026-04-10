namespace BananaSuisa.Core.Winget;

public sealed class WingetSearchOutcome
{
    private WingetSearchOutcome(bool success, string message, IReadOnlyList<WingetSearchItem> items, string? failureDetail)
    {
        Success = success;
        Message = message;
        Items = items;
        FailureDetail = failureDetail;
    }

    public bool Success { get; }

    public string Message { get; }

    public IReadOnlyList<WingetSearchItem> Items { get; }

    /// <summary>
    /// Saida bruta truncada para diagnostico (log JSON); nao deve ser mostrada na UI inteira.
    /// </summary>
    public string? FailureDetail { get; }

    public static WingetSearchOutcome Ok(string message, IReadOnlyList<WingetSearchItem> items) =>
        new(true, message, items, null);

    public static WingetSearchOutcome Fail(string message, string? failureDetail = null) =>
        new(false, message, [], failureDetail);
}
