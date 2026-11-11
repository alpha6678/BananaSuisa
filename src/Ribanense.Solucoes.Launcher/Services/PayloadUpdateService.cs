using System.IO;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.PluginSDK.Payload;

namespace Ribanense.Solucoes.Launcher.Services;

public enum PayloadStageKind
{
    Full,
    Chain
}

public sealed record PayloadStageResult(bool Success, string? Error, PayloadStageKind Kind);

/// <summary>
/// Monta uma pasta de staging com a versão pedida: corrente de deltas quando
/// cada elo existe, senão o zip completo. Um delta inválido também cai no zip.
/// </summary>
public sealed class PayloadUpdateService
{
    private readonly IGitHubClient _github;
    private readonly IAppJsonLog _log;

    public PayloadUpdateService(IGitHubClient github, IAppJsonLog log)
    {
        _github = github ?? throw new ArgumentNullException(nameof(github));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<PayloadStageResult> StageAsync(
        string? installedDir,
        string? installedVersion,
        ReleaseInfo target,
        IReadOnlyList<ReleaseInfo> history,
        string stagingDir,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (string.IsNullOrWhiteSpace(stagingDir)) throw new ArgumentException("Staging obrigatório.", nameof(stagingDir));

        var releases = (history ?? Array.Empty<ReleaseInfo>())
            .Where(r => !r.IsPrerelease || string.Equals(r.Version, target.Version, StringComparison.Ordinal))
            .Append(target)
            .ToList();

        var plan = PayloadPlanner.Plan(
            installedVersion,
            target.Version,
            releases.Select(r => r.ToPayloadRef()).ToList());

        if (plan.Kind == PayloadPlanKind.Chain &&
            plan.Hops.Count > 0 &&
            !string.IsNullOrWhiteSpace(installedDir) &&
            Directory.Exists(installedDir))
        {
            try
            {
                PayloadApplier.ResetDirectory(stagingDir);
                string sourceDir = installedDir!;
                PayloadApplier.CopyTree(sourceDir, stagingDir);
                int hopIndex = 0;
                foreach (DeltaHop hop in plan.Hops)
                {
                    hopIndex++;
                    ReleaseInfo? release = releases.FirstOrDefault(r =>
                        string.Equals(r.Version, hop.ToVersion, StringComparison.Ordinal));
                    if (release is null)
                        throw new InvalidOperationException($"Release {hop.ToVersion} ausente na corrente.");

                    await ApplyHopAsync(stagingDir, release, hop.FromVersion, progress, ct).ConfigureAwait(false);
                    progress?.Report((double)hopIndex / plan.Hops.Count);
                }

                VerifyStagedManifest(stagingDir, target.Version);
                _log.Write(AppLogLevel.Information, "payload.delta",
                    $"Atualização {installedVersion} → {target.Version} em {plan.Hops.Count} delta(s).");
                return new PayloadStageResult(true, null, PayloadStageKind.Chain);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.Write(AppLogLevel.Warning, "payload.delta",
                    "Delta indisponível ou inválido; baixando o pacote completo.", ex);
                PayloadApplier.ResetDirectory(stagingDir);
            }
        }
        else
        {
            PayloadApplier.ResetDirectory(stagingDir);
        }

        if (target.ZipAsset is null)
        {
            return new PayloadStageResult(false,
                $"Release {target.Tag} não tem asset .zip.",
                PayloadStageKind.Full);
        }

        try
        {
            byte[] zipBytes = await DownloadVerifiedAsync(
                target.ZipAsset, target.Sha256Asset, progress, ct).ConfigureAwait(false);
            PayloadApplier.ExtractZip(zipBytes, stagingDir);
            if (!string.IsNullOrWhiteSpace(installedDir))
                PayloadApplier.PreserveSidecars(installedDir, stagingDir);
            VerifyStagedManifest(stagingDir, target.Version);
            return new PayloadStageResult(true, null, PayloadStageKind.Full);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PayloadApplier.ResetDirectory(stagingDir);
            try { Directory.Delete(stagingDir, recursive: true); } catch { }
            _log.Write(AppLogLevel.Error, "payload.full", "Falha ao preparar o pacote completo.", ex);
            return new PayloadStageResult(false, ex.Message, PayloadStageKind.Full);
        }
    }

    private async Task ApplyHopAsync(
        string stagingDir,
        ReleaseInfo release,
        string fromVersion,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        ReleaseAsset? zipAsset = release.DeltaZipFrom(fromVersion);
        ReleaseAsset? manifestAsset = release.DeltaManifestFrom(fromVersion);
        if (zipAsset is null || manifestAsset is null)
            throw new InvalidOperationException($"Delta de {fromVersion} para {release.Version} incompleto.");

        byte[] zipBytes = await DownloadVerifiedAsync(
            zipAsset, release.DeltaShaFrom(fromVersion), progress, ct).ConfigureAwait(false);
        string manifestJson = await _github.GetStringAsync(manifestAsset.DownloadUrl, ct).ConfigureAwait(false);
        DeltaManifest manifest = DeltaManifest.Parse(manifestJson);
        if (!string.Equals(manifest.FromVersion, fromVersion, StringComparison.Ordinal) ||
            !string.Equals(manifest.ToVersion, release.Version, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Delta anunciado {fromVersion} → {release.Version}, manifesto {manifest.FromVersion} → {manifest.ToVersion}.");
        }

        PayloadApplier.ApplyDelta(stagingDir, zipBytes, manifest);
    }

    private async Task<byte[]> DownloadVerifiedAsync(
        ReleaseAsset asset,
        ReleaseAsset? shaAsset,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        byte[] bytes = await _github.GetBytesAsync(asset.DownloadUrl, progress, ct).ConfigureAwait(false);
        if (shaAsset is null) return bytes;

        try
        {
            byte[] shaBytes = await _github.GetBytesAsync(shaAsset.DownloadUrl, null, ct).ConfigureAwait(false);
            string expected = Sha256Util.ExtractHash(System.Text.Encoding.UTF8.GetString(shaBytes).Trim());
            string actual = Sha256Util.Compute(bytes);
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"SHA256 de {asset.Name} não confere. Esperado {expected}, obtido {actual}.");
            }
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Warning, "payload.hash",
                $"Falha ao validar SHA256 de {asset.Name} (prosseguindo sem validação).", ex);
        }

        return bytes;
    }

    private static void VerifyStagedManifest(string stagingDir, string expectedVersion)
    {
        string path = Path.Combine(stagingDir, PayloadManifest.FileName);
        if (!File.Exists(path)) return;

        PayloadManifest manifest = PayloadManifest.Load(path);
        if (!string.IsNullOrWhiteSpace(manifest.Version) &&
            !string.Equals(manifest.Version, expectedVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"payload-manifest.json está em {manifest.Version}, esperado {expectedVersion}.");
        }

        IReadOnlyList<string> errors = PayloadApplier.Verify(stagingDir, manifest);
        if (errors.Count > 0)
            throw new InvalidDataException("Payload não confere: " + string.Join("; ", errors));
    }
}
