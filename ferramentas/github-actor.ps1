#Requires -Version 5.1
# Commit e release saem na conta do gh com escrita que a pessoa escolhe no terminal, uma vez por comando.
# O nome e o do perfil dessa conta. O e-mail e o noreply do GitHub, ligado a esse login.

$script:GithubActorProjectRoot = Split-Path -Parent $PSScriptRoot

function Get-GithubOwnerRepo {
    param([Parameter(Mandatory)] [string] $ProjectRoot)
    Push-Location $ProjectRoot
    try {
        $url = & git remote get-url origin 2>$null
        if ($LASTEXITCODE -ne 0 -or $url -notmatch 'github\.com[:/]([^/]+)/([^/.]+)') {
            throw "O remote origin nao e um repositorio GitHub. A publicacao usa o origin, sem dono fixo na CLI."
        }
        return [pscustomobject]@{ Owner = $Matches[1]; Repo = $Matches[2] }
    } finally {
        Pop-Location
    }
}

function Invoke-GhQuiet {
    param(
        [Parameter(Mandatory)] [string[]] $GhArgs,
        [string] $Token
    )
    $ErrorActionPreference = 'Continue'
    $saved = [Environment]::GetEnvironmentVariable('GH_TOKEN', 'Process')
    try {
        if ($Token) { [Environment]::SetEnvironmentVariable('GH_TOKEN', $Token, 'Process') }
        $out = & gh @GhArgs 2>$null
        if ($LASTEXITCODE -ne 0) { return $null }
        return ($out | Out-String).Trim()
    }
    finally {
        [Environment]::SetEnvironmentVariable('GH_TOKEN', $saved, 'Process')
    }
}

function Get-GhAccount {
    param(
        [Parameter(Mandatory)] [string] $Repo,
        [string] $Token
    )
    $raw = Invoke-GhQuiet -Token $Token -GhArgs @('api', 'user', '--jq', '{login:.login,name:.name,id:.id}')
    if (-not $raw) { return $null }
    $user = $raw | ConvertFrom-Json
    $push = Invoke-GhQuiet -Token $Token -GhArgs @('api', "repos/$Repo", '--jq', '.permissions.push')
    return [pscustomobject]@{
        Login   = [string] $user.login
        Name    = [string] $user.name
        Id      = [string] $user.id
        Token   = $Token
        CanPush = ($push -eq 'true')
    }
}

function Get-GhLogins {
    $raw = Invoke-GhQuiet -GhArgs @('auth', 'status', '--hostname', 'github.com', '--json', 'hosts')
    if (-not $raw) { return @() }
    $status = $raw | ConvertFrom-Json
    return @($status.hosts.'github.com' | Where-Object { $_.state -eq 'success' } | ForEach-Object { [string] $_.login })
}

function Get-ProfileName {
    param($Account)
    $name = ($Account.Name -replace '[\r\n"]', ' ').Trim()
    if ([string]::IsNullOrWhiteSpace($name)) { return $Account.Login }
    return $name
}

function Read-GhAccountChoice {
    param(
        [Parameter(Mandatory)] [object[]] $Accounts,
        [Parameter(Mandatory)] [string] $Repo,
        [string] $PreferredLogin
    )

    $accounts = @($Accounts)
    $preferred = 1
    Write-Host ""
    Write-Host "Contas com escrita em ${Repo}:" -ForegroundColor Cyan
    for ($i = 0; $i -lt $accounts.Count; $i++) {
        $account = $accounts[$i]
        $profile = Get-ProfileName -Account $account
        $label = if ($profile -eq $account.Login) { $account.Login } else { "$profile ($($account.Login))" }
        $mark = ""
        if ($account.Login -eq $PreferredLogin) {
            $preferred = $i + 1
            $mark = " [ativa]"
        }
        Write-Host ("  {0}) {1}{2}" -f ($i + 1), $label, $mark)
    }

    while ($true) {
        $answer = Read-Host "Numero da conta que deve assinar o commit [$preferred]"
        if ([string]::IsNullOrWhiteSpace($answer)) { return $accounts[$preferred - 1] }
        $n = 0
        if ([int]::TryParse($answer.Trim(), [ref]$n) -and $n -ge 1 -and $n -le $accounts.Count) {
            return $accounts[$n - 1]
        }
        Write-Host "Escolha um numero de 1 a $($accounts.Count)." -ForegroundColor Yellow
    }
}

function Resolve-SelectedAccount {
    param([Parameter(Mandatory)] [string] $Repo)

    $forcedToken = [bool]($env:GH_TOKEN -or $env:GITHUB_TOKEN)
    $active = Get-GhAccount -Repo $Repo
    if (-not $active) {
        throw "Nao foi possivel ler a conta do gh. Rode 'gh auth status'. Veja docs/IDENTIDADE_GITHUB.md."
    }

    $candidates = @()
    if ($active.CanPush) { $candidates += $active }
    if (-not $forcedToken) {
        foreach ($other in @(Get-GhLogins | Where-Object { $_ -ne $active.Login })) {
            $token = Invoke-GhQuiet -GhArgs @('auth', 'token', '--hostname', 'github.com', '--user', $other)
            if (-not $token) { continue }
            $candidate = Get-GhAccount -Repo $Repo -Token $token
            if ($candidate -and $candidate.CanPush -and $candidate.Login -ne $active.Login) {
                $candidates += $candidate
            }
        }
    }
    elseif (-not $active.CanPush) {
        throw "A conta do GH_TOKEN/GITHUB_TOKEN ($($active.Login)) nao tem escrita em $Repo. Apague a variavel nesta sessao ou aponte para um token com escrita. Veja docs/IDENTIDADE_GITHUB.md."
    }
    if ($candidates.Count -eq 0) {
        throw "Nenhuma conta logada no gh tem escrita em $Repo. Entre com a sua conta ('gh auth login'). Veja docs/IDENTIDADE_GITHUB.md."
    }

    $remembered = @($candidates | Where-Object { $_.Login -eq $env:RIBANENSE_GH_LOGIN })
    if ($remembered.Count -eq 1) {
        return [pscustomobject]@{ Account = $remembered[0]; Note = $null }
    }

    $chosen = Read-GhAccountChoice -Accounts @($candidates) -Repo $Repo -PreferredLogin $active.Login
    $env:RIBANENSE_GH_LOGIN = $chosen.Login
    $note = $null
    if ($chosen.Login -ne $active.Login) {
        $note = "A conta ativa do gh e $($active.Login). O publish usa $($chosen.Login)."
    }
    return [pscustomobject]@{ Account = $chosen; Note = $note }
}

function Get-GithubActor {
    if ($script:GithubActor) { return $script:GithubActor }

    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "gh nao encontrado no PATH. Instale a GitHub CLI e entre com a conta que deve assinar o publish. Veja docs/IDENTIDADE_GITHUB.md."
    }

    $origin = Get-GithubOwnerRepo -ProjectRoot $script:GithubActorProjectRoot
    $repo = "$($origin.Owner)/$($origin.Repo)"

    $selected = Resolve-SelectedAccount -Repo $repo
    $account = $selected.Account
    $name = Get-ProfileName -Account $account
    $email = "$($account.Id)+$($account.Login)@users.noreply.github.com"
    $note = $selected.Note

    if ($account.Login -notmatch '^[A-Za-z0-9-]+$') {
        throw "Login do gh invalido para assinar o commit: '$($account.Login)'."
    }
    if ($account.Id -notmatch '^\d+$') {
        throw "Id da conta gh ausente para '$($account.Login)'."
    }

    # Fora do objeto para nao aparecer se alguem imprimir o ator.
    $script:GithubActorToken = $account.Token
    $script:GithubActor = [pscustomobject]@{
        Login = $account.Login
        Name  = $name
        Id    = $account.Id
        Email = $email
        Note  = $note
    }
    return $script:GithubActor
}

function Invoke-GitAsActor {
    param(
        [Parameter(Mandatory)] [string[]] $GitArgs,
        [string] $FailureMessage = 'Comando git da publicacao falhou.'
    )

    $actor = Get-GithubActor
    $keys = @('GIT_AUTHOR_NAME', 'GIT_AUTHOR_EMAIL', 'GIT_COMMITTER_NAME', 'GIT_COMMITTER_EMAIL', 'GH_TOKEN')
    $saved = @{}
    foreach ($key in $keys) {
        $saved[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
    }

    $code = 1
    try {
        [Environment]::SetEnvironmentVariable('GIT_AUTHOR_NAME', $actor.Name, 'Process')
        [Environment]::SetEnvironmentVariable('GIT_AUTHOR_EMAIL', $actor.Email, 'Process')
        [Environment]::SetEnvironmentVariable('GIT_COMMITTER_NAME', $actor.Name, 'Process')
        [Environment]::SetEnvironmentVariable('GIT_COMMITTER_EMAIL', $actor.Email, 'Process')
        if ($script:GithubActorToken) {
            [Environment]::SetEnvironmentVariable('GH_TOKEN', $script:GithubActorToken, 'Process')
        }

        # -c grava o nome e o e-mail da conta escolhida. O helper usa o token dessa conta.
        $prefixed = @(
            '-c', "user.name=$($actor.Name)",
            '-c', "user.email=$($actor.Email)",
            '-c', "credential.https://github.com.username=$($actor.Login)",
            '-c', 'credential.helper=',
            '-c', 'credential.helper=!gh auth git-credential'
        ) + $GitArgs

        & git @prefixed
        $code = $LASTEXITCODE
    }
    finally {
        foreach ($key in $keys) {
            [Environment]::SetEnvironmentVariable($key, $saved[$key], 'Process')
        }
    }

    if ($code -ne 0) { throw $FailureMessage }
}

function Invoke-GhAsActor {
    param(
        [Parameter(Mandatory)] [string[]] $GhArgs,
        [string] $FailureMessage = 'Comando gh da publicacao falhou.'
    )

    Get-GithubActor | Out-Null
    $saved = [Environment]::GetEnvironmentVariable('GH_TOKEN', 'Process')
    $code = 1
    try {
        if ($script:GithubActorToken) {
            [Environment]::SetEnvironmentVariable('GH_TOKEN', $script:GithubActorToken, 'Process')
        }
        & gh @GhArgs
        $code = $LASTEXITCODE
    }
    finally {
        [Environment]::SetEnvironmentVariable('GH_TOKEN', $saved, 'Process')
    }

    if ($code -ne 0) { throw $FailureMessage }
}
