using System.Text;
using BananaSuisa.Core.Workspace;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.Infrastructure.Workspace;

public sealed class WorkspaceBootstrapService : IWorkspaceBootstrapService
{
    public WorkspaceBootstrapResult EnsureInitialized(WorkspacePaths paths)
    {
        List<WorkspaceBootstrapItem> items = [];
        int createdDirectoryCount = 0;
        int synchronizedFileCount = 0;

        foreach ((string name, string directoryPath) in EnumerateDirectories(paths))
        {
            bool existed = Directory.Exists(directoryPath);

            try
            {
                Directory.CreateDirectory(directoryPath);
                if (!existed)
                {
                    createdDirectoryCount++;
                }

                string detail = existed
                    ? $"{directoryPath} ja existia."
                    : $"{directoryPath} foi criada.";
                items.Add(new WorkspaceBootstrapItem(name, true, detail));
            }
            catch (Exception ex)
            {
                items.Add(new WorkspaceBootstrapItem(name, false, $"{directoryPath} falhou: {ex.Message}"));
            }
        }

        synchronizedFileCount += SyncFileIfMissing(
            "Configuracao base",
            paths.PayloadConfigPath,
            paths.ConfigPath,
            items);

        synchronizedFileCount += SyncFileIfMissing(
            "Catalogo base de instalacao",
            paths.PayloadInstallCatalogPath,
            paths.InstallCatalogPath,
            items);

        synchronizedFileCount += SyncFileIfMissing(
            "Catalogo base tecnico",
            paths.PayloadTechCatalogPath,
            paths.TechCatalogPath,
            items);

        synchronizedFileCount += EnsureReadme(paths.ReadmePath, items);

        items.Add(new WorkspaceBootstrapItem(
            "Arquivo de log",
            true,
            $"O log principal desta base .NET sera escrito em {paths.LogFilePath} quando o servico de log for implementado."));

        return new WorkspaceBootstrapResult(paths, items, createdDirectoryCount, synchronizedFileCount);
    }

    private static IEnumerable<(string Name, string Path)> EnumerateDirectories(WorkspacePaths paths)
    {
        yield return ("Memoria", paths.MemoryRoot);
        yield return ("Registros", paths.LogsRoot);
        yield return ("Dados", paths.DataRoot);
        yield return ("Perfis", paths.ProfilesRoot);
        yield return ("Scripts extras", paths.ScriptsRoot);
        yield return ("Temporarios", paths.TempRoot);
        yield return ("Drivers", paths.DriversRoot);
        yield return ("Pacotes baixados", paths.InstallersRoot);
        yield return ("Cache WinGet", paths.WingetCacheRoot);
    }

    private static int SyncFileIfMissing(string name, string sourcePath, string targetPath, ICollection<WorkspaceBootstrapItem> items)
    {
        if (!File.Exists(sourcePath))
        {
            items.Add(new WorkspaceBootstrapItem(name, false, $"Origem ausente: {sourcePath}"));
            return 0;
        }

        if (File.Exists(targetPath))
        {
            items.Add(new WorkspaceBootstrapItem(name, true, $"{targetPath} ja existe; nenhuma copia foi necessaria."));
            return 0;
        }

        try
        {
            string? targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            File.Copy(sourcePath, targetPath, overwrite: false);
            items.Add(new WorkspaceBootstrapItem(name, true, $"{Path.GetFileName(sourcePath)} foi sincronizado para {targetPath}."));
            return 1;
        }
        catch (Exception ex)
        {
            items.Add(new WorkspaceBootstrapItem(name, false, $"{targetPath} falhou: {ex.Message}"));
            return 0;
        }
    }

    private static int EnsureReadme(string readmePath, ICollection<WorkspaceBootstrapItem> items)
    {
        if (File.Exists(readmePath))
        {
            items.Add(new WorkspaceBootstrapItem("LEIA-ME da memoria", true, $"{readmePath} ja existe."));
            return 0;
        }

        const string content = """
BananaSuisa - pasta de memoria (dentro de BananaSuisa_recursos)
================================================================

Fica em: BananaSuisa_recursos\BananaSuisa_memoria (junto ao projeto).

Aqui ficam configuracoes, catalogos em uso, o arquivo de log (JSON) e arquivos
baixados. Os JSONs na raiz de BananaSuisa_recursos sao apenas modelos; as copias
de trabalho ficam em Dados\.

Subpastas:
  Registros          - Arquivo BananaSuisa.json (log da sessao) e outros diagnosticos.
  Dados              - Configuracao e catalogos (copiados dos modelos na primeira vez).
  Perfis             - Perfis de aplicativos para instalacao em lote.
  ScriptsExtras      - Scripts auxiliares.
  Temporarios        - Arquivos temporarios (pode limpar com o app fechado).
  DriversImpressoras - Drivers de impressora para instalacao.
  PacotesBaixados    - Instaladores guardados; WinGet = cache do WinGet.

Para redefinir o app aos padroes, apague esta pasta (com o programa fechado) e
execute de novo - os arquivos-base serao copiados de BananaSuisa_recursos.
""";

        try
        {
            File.WriteAllText(readmePath, content.TrimEnd(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            items.Add(new WorkspaceBootstrapItem("LEIA-ME da memoria", true, $"{readmePath} foi criado."));
            return 1;
        }
        catch (Exception ex)
        {
            items.Add(new WorkspaceBootstrapItem("LEIA-ME da memoria", false, $"{readmePath} falhou: {ex.Message}"));
            return 0;
        }
    }
}
