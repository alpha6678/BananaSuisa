#Requires -Version 5.1
<#
.SYNOPSIS
  Gera payload-manifest.json e, se houver a versao anterior, o zip de delta.

.DESCRIPTION
  O manifesto lista cada arquivo do payload com tamanho e SHA256, em caminho
  relativo com barra normal, ordenado. Nao inclui o proprio manifesto, nem
  dados/, aplicativos/, portable.flag ou install-mode.txt.

  O delta (delta-from-<versao>.zip) leva so os arquivos adicionados ou alterados
  e sempre o payload-manifest.json novo. O json irmao lista esses arquivos e os
  removidos. A regra e a mesma de PayloadManifest/DeltaManifest no PluginSDK.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $TargetDir,
    [Parameter(Mandatory = $true)] [string] $Version,
    [Parameter(Mandatory = $true)] [string] $EntryExecutable,
    [string] $PreviousDir,
    [string] $FromVersion,
    [string] $OutputDir
)

$ErrorActionPreference = 'Stop'

function Get-PayloadRelativePath {
    param([string] $Root, [string] $File)
    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $fileFull = [System.IO.Path]::GetFullPath($File)
    $relative = $fileFull.Substring($rootFull.Length).TrimStart('\', '/')
    return ($relative -replace '\\', '/')
}

function Test-PayloadPath {
    param([string] $Relative)
    if ([string]::IsNullOrWhiteSpace($Relative)) { return $false }
    $normalized = ($Relative -replace '\\', '/').TrimStart('/')
    if ($normalized.Contains(':')) { return $false }
    $parts = @($normalized.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries))
    if ($parts.Count -eq 0) { return $false }
    if ($parts -contains '.' -or $parts -contains '..') { return $false }
    if ($normalized -eq 'payload-manifest.json') { return $false }
    if ($normalized -eq 'portable.flag' -or $normalized -eq 'install-mode.txt') { return $false }
    if ($normalized -eq 'dados' -or $normalized.StartsWith('dados/')) { return $false }
    if ($normalized -eq 'aplicativos' -or $normalized.StartsWith('aplicativos/')) { return $false }
    return $true
}

function Get-PayloadFileEntries {
    param([string] $Root)
    $rootFull = [System.IO.Path]::GetFullPath($Root)
    $entries = @()
    foreach ($file in (Get-ChildItem -LiteralPath $rootFull -Recurse -File -Force)) {
        $relative = Get-PayloadRelativePath -Root $rootFull -File $file.FullName
        if (-not (Test-PayloadPath $relative)) { continue }
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $entries += [pscustomobject]@{
            path   = $relative
            size   = [int64] $file.Length
            sha256 = $hash
        }
    }
    return @($entries | Sort-Object { $_.path })
}

function Format-JsonString {
    param([string] $Value)
    if ($null -eq $Value) { $Value = '' }
    $escaped = $Value.Replace('\', '\\').Replace('"', '\"')
    return '"' + $escaped + '"'
}

function Format-FileEntries {
    param([object[]] $Files)
    if (-not $Files -or $Files.Count -eq 0) { return '[]' }
    $blocks = @(
        foreach ($file in $Files) {
            @"
    {
      "path": $(Format-JsonString $file.path),
      "size": $($file.size),
      "sha256": $(Format-JsonString $file.sha256)
    }
"@
        }
    )
    return "[`n" + ($blocks -join ",`n") + "`n  ]"
}

function Write-PayloadManifest {
    param(
        [string] $Root,
        [string] $ManifestVersion,
        [string] $Entry,
        [object[]] $Files
    )
    $json = @"
{
  "schemaVersion": 1,
  "version": $(Format-JsonString $ManifestVersion),
  "entryExecutable": $(Format-JsonString ($Entry -replace '\\', '/')),
  "files": $(Format-FileEntries $Files)
}

"@
    $path = Join-Path $Root 'payload-manifest.json'
    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($path, $json, $utf8)
    return $path
}

function New-DeltaZip {
    param(
        [string] $Root,
        [string] $ZipPath,
        [object[]] $Files
    )
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path -LiteralPath $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }
    $zip = [System.IO.Compression.ZipFile]::Open($ZipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $seen = @{}
        foreach ($file in $Files) {
            $relative = [string] $file.path
            if ($seen.ContainsKey($relative)) { continue }
            $seen[$relative] = $true
            $source = Join-Path $Root ($relative -replace '/', '\')
            if (-not (Test-Path -LiteralPath $source)) {
                throw "Arquivo do delta ausente: $relative"
            }
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $source, ($relative -replace '\\', '/'), [System.IO.Compression.CompressionLevel]::Optimal)
        }
        $manifest = Join-Path $Root 'payload-manifest.json'
        if ((Test-Path -LiteralPath $manifest) -and -not $seen.ContainsKey('payload-manifest.json')) {
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $manifest, 'payload-manifest.json', [System.IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally {
        $zip.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $TargetDir)) {
    throw "Pasta do payload nao encontrada: $TargetDir"
}

$targetFiles = @(Get-PayloadFileEntries -Root $TargetDir)
$manifestPath = Write-PayloadManifest -Root $TargetDir -ManifestVersion $Version -Entry $EntryExecutable -Files $targetFiles
Write-Host "payload-manifest.json: $($targetFiles.Count) arquivo(s)"

if ($OutputDir) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $OutputDir 'payload-manifest.json') -Force
}

if (-not $PreviousDir -or -not $FromVersion) {
    return
}
if (-not (Test-Path -LiteralPath $PreviousDir)) {
    throw "Pasta da versao anterior nao encontrada: $PreviousDir"
}

$previousFiles = @(Get-PayloadFileEntries -Root $PreviousDir)
$previousByPath = @{}
foreach ($file in $previousFiles) { $previousByPath[$file.path] = $file }
$targetByPath = @{}
foreach ($file in $targetFiles) { $targetByPath[$file.path] = $file }

$changed = @()
foreach ($file in $targetFiles) {
    $old = $previousByPath[$file.path]
    if (-not $old -or $old.sha256 -ne $file.sha256 -or $old.size -ne $file.size) {
        $changed += $file
    }
}
$deleted = @($previousFiles | Where-Object { -not $targetByPath.ContainsKey($_.path) } | ForEach-Object { $_.path } | Sort-Object)

$deltaName = "delta-from-$FromVersion"
$deltaDir = if ($OutputDir) { $OutputDir } else { $TargetDir }
New-Item -ItemType Directory -Path $deltaDir -Force | Out-Null

$deletedJson = if (-not $deleted -or $deleted.Count -eq 0) {
    '[]'
} else {
    $items = @(foreach ($path in $deleted) { '    ' + (Format-JsonString $path) })
    "[`n" + ($items -join ",`n") + "`n  ]"
}
$deltaJson = @"
{
  "schemaVersion": 1,
  "fromVersion": $(Format-JsonString $FromVersion),
  "toVersion": $(Format-JsonString $Version),
  "files": $(Format-FileEntries $changed),
  "deleted": $deletedJson
}

"@
$deltaJsonPath = Join-Path $deltaDir "$deltaName.json"
$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($deltaJsonPath, $deltaJson, $utf8)

$deltaZipPath = Join-Path $deltaDir "$deltaName.zip"
New-DeltaZip -Root $TargetDir -ZipPath $deltaZipPath -Files $changed
$hash = (Get-FileHash -LiteralPath $deltaZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$zipName = Split-Path -Leaf $deltaZipPath
"$hash  $zipName" | Set-Content -LiteralPath "$deltaZipPath.sha256" -Encoding ASCII

Write-Host "delta ${FromVersion} -> ${Version}: $($changed.Count) arquivo(s), $($deleted.Count) remocao(oes)"
