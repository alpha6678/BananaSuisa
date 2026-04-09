namespace BananaSuisa.Core.Configuration;

public sealed record ConfigurationLoadResult(
    bool Succeeded,
    string SourcePath,
    BananaSuisaConfig? Configuration,
    string Detail)
{
    public string Summary =>
        !Succeeded || Configuration is null
            ? Detail
            : $"Versao {Configuration.Version} | Perfis {Configuration.Profiles.Count} | Perfil padrao {Configuration.DefaultProfile}";
}
