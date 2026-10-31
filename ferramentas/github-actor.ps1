#Requires -Version 5.1
# Identidade de publicacao: uma conta do gh com escrita no origin, nao o user.email da maquina.
# Ordem: conta ativa do gh (ou GH_TOKEN/GITHUB_TOKEN da sessao); sem escrita, outra conta logada no gh.

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

function Get-GithubActor {
    if ($script:GithubActor) { return $script:GithubActor }

    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "gh nao encontrado no PATH. Instale a GitHub CLI e entre com a conta que deve assinar o publish. Veja docs/IDENTIDADE_GITHUB.md."
    }

    $origin = Get-GithubOwnerRepo -ProjectRoot $script:GithubActorProjectRoot
    $repo = "$($origin.Owner)/$($origin.Repo)"

    $active = Get-GhAccount -Repo $repo
    if (-not $active) {
        throw "Nao foi possivel ler a conta ativa do gh. Rode 'gh auth status' e entre com a sua conta ('gh auth login' ou 'gh auth switch'). Veja docs/IDENTIDADE_GITHUB.md."
    }

    $account = $active
    $note = $null
    if (-not $active.CanPush) {
        if ($env:GH_TOKEN -or $env:GITHUB_TOKEN) {
            throw "O token de GH_TOKEN/GITHUB_TOKEN ($($active.Login)) nao tem escrita em $repo. Apague a variavel na sessao ou aponte para um token com escrita. Veja docs/IDENTIDADE_GITHUB.md."
        }
        $account = $null
        foreach ($other in @(Get-GhLogins | Where-Object { $_ -ne $active.Login })) {
            $token = Invoke-GhQuiet -GhArgs @('auth', 'token', '--hostname', 'github.com', '--user', $other)
            if (-not $token) { continue }
            $candidate = Get-GhAccount -Repo $repo -Token $token
            if ($candidate -and $candidate.CanPush) {
                $account = $candidate
                break
            }
        }
        if (-not $account) {
            throw "A conta ativa do gh ($($active.Login)) nao tem escrita em $repo, e nenhuma outra conta logada no gh tem. Entre com uma conta com escrita ('gh auth login'). Veja docs/IDENTIDADE_GITHUB.md."
        }
        $note = "A conta ativa do gh ($($active.Login)) nao tem escrita em $repo; o publish usa $($account.Login), tambem logada no gh."
    }

    $login = $account.Login
    if ($login -notmatch '^[A-Za-z0-9-]+$') {
        throw "Login do gh invalido para assinar o commit: '$login'."
    }

    $id = $account.Id
    if ($id -notmatch '^\d+$') {
        throw "Id da conta gh ausente para '$login'."
    }

    $name = ($account.Name -replace '[\r\n"]', ' ').Trim()
    if ([string]::IsNullOrWhiteSpace($name)) { $name = $login }

    # Fora do objeto para nao aparecer se alguem imprimir o ator.
    $script:GithubActorToken = $account.Token
    $script:GithubActor = [pscustomobject]@{
        Login = $login
        Name  = $name
        Id    = $id
        Email = "$id+$login@users.noreply.github.com"
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

        # -c vence o user.email e o credential.username gravados na maquina.
        # helper vazio zera helpers de prioridade menor; o seguinte usa o token da conta escolhida.
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
