namespace Ribanense.Solucoes.Launcher.Services;

public static class AppSlug
{
    public static string FromId(string appId)
    {
        int lastDot = appId.LastIndexOf('.');
        string slug = lastDot >= 0 ? appId[(lastDot + 1)..] : appId;
        if (string.IsNullOrWhiteSpace(slug)) slug = "app";
        return char.ToUpperInvariant(slug[0]) + slug[1..];
    }
}
