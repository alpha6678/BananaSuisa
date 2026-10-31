#Requires -Version 5.1
# rb commit: um commit por area, mensagem automatica, sync com origin.
# Requer github-actor.ps1 ja carregado (Get-GithubActor, Invoke-GitAsActor).

function Get-CommitArea {
    param([Parameter(Mandatory)] [string] $Path)
    $p = $Path.Replace('\', '/')
    if ($p -match '^src/Ribanense\.Solucoes\.PluginSDK/') {
        return [pscustomobject]@{ Scope = 'sdk'; Order = 0 }
    }
    if ($p -match '^src/Ribanense\.Solucoes\.Infrastructure/') {
        return [pscustomobject]@{ Scope = 'infra'; Order = 1 }
    }
    if ($p -match '^src/Ribanense\.Solucoes\.UI/') {
        return [pscustomobject]@{ Scope = 'ui'; Order = 2 }
    }
    if ($p -match '^src/aplicativos/Ribanense\.Solucoes\.App\.([^/]+)/') {
        return [pscustomobject]@{ Scope = $Matches[1].ToLowerInvariant(); Order = 10 }
    }
    if ($p -match '^tests/.+App\.([^/]+)\.Tests/') {
        return [pscustomobject]@{ Scope = $Matches[1].ToLowerInvariant(); Order = 10 }
    }
    if ($p -match '^tests/') {
        return [pscustomobject]@{ Scope = 'test'; Order = 15 }
    }
    if ($p -match '^src/Ribanense\.Solucoes\.Launcher/') {
        return [pscustomobject]@{ Scope = 'launcher'; Order = 20 }
    }
    if ($p -match '^firmware/') {
        return [pscustomobject]@{ Scope = 'firmware'; Order = 30 }
    }
    if ($p -match '^ferramentas/') {
        return [pscustomobject]@{ Scope = 'cli'; Order = 40 }
    }
    if ($p -match '^catalog/') {
        return [pscustomobject]@{ Scope = 'catalog'; Order = 50 }
    }
    if ($p -match '^docs/' -or $p -match '(^|/)\.cursor/' -or $p -match '\.md$') {
        return [pscustomobject]@{ Scope = 'docs'; Order = 60 }
    }
    return [pscustomobject]@{ Scope = 'repo'; Order = 70 }
}

function Test-CommitSecretPath {
    param([Parameter(Mandatory)] [string] $Path)
    $n = $Path.Replace('\', '/')
    if ($n -match '(^|/)\.env($|\.)') { return $true }
    if ($n -match '(^|/)secrets/') { return $true }
    if ($n -match '\.(pem|pfx|key)$') { return $true }
    return $false
}

function Get-GitStatusEntries {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = (Get-Command git).Source
    $psi.Arguments = '-c core.quotepath=false status --porcelain=v1 -z --untracked-files=all'
    $psi.WorkingDirectory = $script:ProjectRoot
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $utf8 = New-Object System.Text.UTF8Encoding $false
    $psi.StandardOutputEncoding = $utf8
    $psi.StandardErrorEncoding = $utf8
    $proc = [System.Diagnostics.Process]::Start($psi)
    $text = $proc.StandardOutput.ReadToEnd()
    $err = $proc.StandardError.ReadToEnd()
    $proc.WaitForExit()
    if ($proc.ExitCode -ne 0) {
        throw "git status falhou. $err"
    }
    if ([string]::IsNullOrEmpty($text)) { return @() }

    $parts = $text.Split([char]0)
    $entries = @()
    $i = 0
    while ($i -lt $parts.Count) {
        $rec = $parts[$i]
        if ([string]::IsNullOrEmpty($rec)) { $i++; continue }
        if ($rec.Length -lt 4) {
            throw "Registro de status inesperado."
        }
        $xy = $rec.Substring(0, 2)
        $firstPath = $rec.Substring(3)
        $path = $firstPath
        $orig = $null
        $isRename = ($xy[0] -eq 'R' -or $xy[0] -eq 'C' -or $xy[1] -eq 'R' -or $xy[1] -eq 'C')
        if ($isRename) {
            $i++
            if ($i -ge $parts.Count -or [string]::IsNullOrEmpty($parts[$i])) {
                throw "Rename sem caminho novo."
            }
            $orig = $firstPath.Replace('\', '/')
            $path = $parts[$i]
        }
        $entries += [pscustomobject]@{
            XY   = $xy
            Path = $path.Replace('\', '/')
            Orig = $orig
        }
        $i++
    }
    return @($entries)
}

function Get-CommitGroups {
    param([Parameter(Mandatory)] [object[]] $Entries)
    $groups = @{}
    foreach ($entry in $Entries) {
        $area = Get-CommitArea -Path $entry.Path
        if (-not $groups.ContainsKey($area.Scope)) {
            $groups[$area.Scope] = [pscustomobject]@{
                Scope   = $area.Scope
                Order   = $area.Order
                Entries = @()
            }
        }
        $bucket = $groups[$area.Scope]
        $bucket.Entries = @($bucket.Entries) + $entry
    }
    return @($groups.Values | Sort-Object Order, Scope)
}

function Get-CommitStem {
    param([Parameter(Mandatory)] [string] $Path)
    $name = ($Path.Replace('\', '/').Split('/'))[-1]
    return [System.IO.Path]::GetFileNameWithoutExtension($name)
}

function New-CommitMessage {
    param([Parameter(Mandatory)] $Group)
    $entries = @($Group.Entries)
    $paths = @($entries | ForEach-Object { $_.Path } | Sort-Object)
    $allMd = @($paths | Where-Object { $_ -notmatch '\.md$' }).Count -eq 0 -and $paths.Count -gt 0

    $type = 'fix'
    if ($Group.Scope -eq 'docs' -or $allMd) {
        $type = 'docs'
    }
    elseif ($Group.Scope -eq 'test') {
        $type = 'test'
    }
    else {
        $hasNew = $false
        $onlyRename = $true
        $onlyDelete = $true
        foreach ($entry in $entries) {
            $xy = $entry.XY
            $isNew = ($xy -eq '??' -or $xy[0] -eq 'A' -or $xy[1] -eq 'A')
            $isRename = ($xy[0] -eq 'R' -or $xy[0] -eq 'C' -or $xy[1] -eq 'R' -or $xy[1] -eq 'C')
            $isDelete = ($xy[0] -eq 'D' -or $xy[1] -eq 'D')
            if ($isNew) { $hasNew = $true }
            if (-not $isRename) { $onlyRename = $false }
            if (-not $isDelete -or $isNew -or $isRename) { $onlyDelete = $false }
        }
        if ($hasNew) { $type = 'feat' }
        elseif ($onlyRename) { $type = 'refactor' }
        elseif ($onlyDelete) { $type = 'chore' }
    }

    $verb = switch ($type) {
        'feat'     { 'adicionar em' }
        'docs'     { 'atualizar' }
        'test'     { 'cobrir' }
        'refactor' { 'reorganizar' }
        'chore'    { 'manter' }
        default    { 'corrigir' }
    }

    $stems = @($paths | ForEach-Object { Get-CommitStem $_ } | Select-Object -Unique)
    $shown = @($stems | Select-Object -First 2)
    $titleNames = $shown -join ', '
    if ($stems.Count -gt 2) {
        $titleNames += " +$($stems.Count - 2)"
    }
    $title = '{0}({1}): {2} {3}' -f $type, $Group.Scope, $verb, $titleNames
    $bodyLines = @(
        foreach ($entry in $entries) {
            if ($entry.Orig) { "- $($entry.Orig)" }
            "- $($entry.Path)"
        }
    )
    $body = (@($bodyLines | Select-Object -Unique) -join "`n")
    return [pscustomobject]@{ Title = $title; Body = $body; Paths = $paths }
}

function Invoke-RepoCommit {
    $dryRun = $false
    foreach ($arg in $script:RestArguments) {
        switch ($arg.ToLowerInvariant()) {
            '--dry-run' { $dryRun = $true; continue }
            '--whatif'  { $dryRun = $true; continue }
            '-whatif'   { $dryRun = $true; continue }
            default {
                throw "Argumento desconhecido em commit: '$arg'. Use --dry-run."
            }
        }
    }

    Assert-CommandAvailable 'git'
    Assert-CommandAvailable 'gh'

    Push-Location $script:ProjectRoot
    try {
        $actor = Get-GithubActor
        Write-Info "Conta do commit: $($actor.Login) ($($actor.Name) <$($actor.Email)>)."

        Write-Info "Atualizando referencias (git fetch origin)..."
        Invoke-GitAsActor -GitArgs @('fetch', 'origin') -FailureMessage "git fetch origin falhou."

        $upstream = (& git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>$null | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($upstream)) {
            throw "Este branch nao rastreia um remoto. Defina o upstream antes de rb commit."
        }

        $countRaw = (& git rev-list --left-right --count 'HEAD...@{u}' | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($countRaw)) {
            throw "Nao foi possivel comparar com o remoto."
        }
        $countParts = @($countRaw -split '\s+' | Where-Object { $_ })
        $ahead = [int]$countParts[0]
        $behind = [int]$countParts[1]
        if ($behind -gt 0 -and $ahead -gt 0) {
            throw "origin tem alteracoes que exigem merge; resolva os conflitos e rode de novo."
        }
        if ($behind -gt 0) {
            if ($dryRun) {
                Write-Info "Fast-forward de $behind commit(s) seria aplicado."
            }
            else {
                Write-Info "Aplicando fast-forward ($behind)..."
                Invoke-GitAsActor -GitArgs @('merge', '--ff-only', '@{u}') -FailureMessage "origin tem alteracoes que exigem merge; resolva os conflitos e rode de novo."
            }
        }

        $entries = @(Get-GitStatusEntries)
        if ($entries.Count -eq 0) {
            Write-Ok "Nada para commitar."
            if (-not $dryRun -and $ahead -gt 0 -and $behind -eq 0) {
                Write-Info "Enviando commits para origin..."
                Invoke-GitAsActor -GitArgs @('push', 'origin', 'HEAD') -FailureMessage "git push origin HEAD falhou."
                Write-Ok "origin atualizado."
            }
            return
        }

        $secrets = @(
            foreach ($entry in $entries) {
                if (Test-CommitSecretPath -Path $entry.Path) { $entry.Path }
                elseif ($entry.Orig -and (Test-CommitSecretPath -Path $entry.Orig)) { $entry.Orig }
            }
        )
        if ($secrets.Count -gt 0) {
            foreach ($secret in ($secrets | Select-Object -Unique)) {
                Write-Err "Segredo fora do commit: $secret"
            }
            throw "Ha arquivo de segredo nas alteracoes. Nada foi commitado."
        }

        $groups = @(Get-CommitGroups -Entries $entries)
        Write-Step "Plano de commits"
        foreach ($group in $groups) {
            $msg = New-CommitMessage -Group $group
            Write-Host $msg.Title
            foreach ($line in ($msg.Body -split "`n")) {
                Write-Muted $line
            }
        }

        if ($dryRun) {
            Write-Host ""
            Write-Ok "Dry-run: nenhum commit foi criado e nada foi enviado."
            return
        }

        $report = @()
        foreach ($group in $groups) {
            $msg = New-CommitMessage -Group $group
            $toAdd = @()
            foreach ($entry in $group.Entries) {
                $toAdd += $entry.Path
                if ($entry.Orig) { $toAdd += $entry.Orig }
            }
            $toAdd = @($toAdd | Select-Object -Unique)
            & git add -- @toAdd
            if ($LASTEXITCODE -ne 0) { throw "git add falhou." }
            Invoke-GitAsActor -GitArgs @('commit', '-m', $msg.Title, '-m', $msg.Body) -FailureMessage "git commit falhou."
            $sha = (& git rev-parse --short HEAD | Out-String).Trim()
            $report += "$sha $($msg.Title)"
            Write-Ok $msg.Title
        }

        Write-Info "Enviando commits para origin..."
        Invoke-GitAsActor -GitArgs @('push', 'origin', 'HEAD') -FailureMessage "git push origin HEAD falhou."

        Write-Step "Relatorio"
        foreach ($line in $report) { Write-Host $line }
        Write-Ok "origin atualizado."
    }
    finally {
        Pop-Location
    }
}
