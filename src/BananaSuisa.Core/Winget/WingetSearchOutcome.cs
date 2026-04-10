namespace BananaSuisa.Core.Winget;

public sealed class WingetSearchOutcome
{
    private WingetSearchOutcome(bool success, string message, IReadOnlyList<WingetSearchItem> items)
    {
        Success = success;
        Message = message;
        Items = items;
    }

    public bool Success { get; }

    public string Message { get; }

    public IReadOnlyList<WingetSearchItem> Items { get; }

    public static WingetSearchOutcome Ok(string message, IReadOnlyList<WingetSearchItem> items) =>
        new(true, message, items);

    public static WingetSearchOutcome Fail(string message) =>
        new(false, message, []);
}
