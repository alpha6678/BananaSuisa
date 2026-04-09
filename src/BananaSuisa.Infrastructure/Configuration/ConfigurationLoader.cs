using System.Text.Json;
using BananaSuisa.Core.Configuration;
using BananaSuisa.Core.Workspace;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.Infrastructure.Configuration;

public sealed class ConfigurationLoader : IConfigurationLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ConfigurationLoadResult Load(WorkspacePaths paths)
    {
        string sourcePath = File.Exists(paths.ConfigPath)
            ? paths.ConfigPath
            : paths.PayloadConfigPath;

        if (!File.Exists(sourcePath))
        {
            return new ConfigurationLoadResult(
                false,
                sourcePath,
                null,
                "Nenhum arquivo de configuracao foi encontrado.");
        }

        try
        {
            string json = File.ReadAllText(sourcePath);
            BananaSuisaConfig? configuration = JsonSerializer.Deserialize<BananaSuisaConfig>(json, JsonOptions);

            if (configuration is null)
            {
                return new ConfigurationLoadResult(
                    false,
                    sourcePath,
                    null,
                    "O arquivo de configuracao nao pode ser desserializado.");
            }

            return new ConfigurationLoadResult(
                true,
                sourcePath,
                configuration,
                "Configuracao carregada com sucesso.");
        }
        catch (Exception ex)
        {
            return new ConfigurationLoadResult(
                false,
                sourcePath,
                null,
                $"Falha ao carregar configuracao: {ex.Message}");
        }
    }
}
