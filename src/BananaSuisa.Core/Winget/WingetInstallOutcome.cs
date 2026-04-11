namespace BananaSuisa.Core.Winget;

public sealed class WingetInstallOutcome
{
    private WingetInstallOutcome(bool success, string message, string? failureDetail, bool isCancelled)
    {
        Success = success;
        Message = message;
        FailureDetail = failureDetail;
        IsCancelled = isCancelled;
    }

    public bool Success { get; }

    public string Message { get; }

    public string? FailureDetail { get; }

    /// <summary>True quando o utilizador cancelou ou o processo winget foi interrompido.</summary>
    public bool IsCancelled { get; }

    public static WingetInstallOutcome Ok(string message) => new(true, message, null, false);

    public static WingetInstallOutcome Fail(string message, string? failureDetail = null) => new(false, message, failureDetail, false);

    public static WingetInstallOutcome Cancelled(string message = "Operacao cancelada.") => new(false, message, null, true);
}
