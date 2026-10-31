#Requires -Version 5.1
# Identidade de publicacao na pasta pessoal: so dionerdfrg3@gmail.com.
# O token do gh e o da conta em credential.https://github.com.username (alpha6678).
# A conta ativa do gh fica de fora quando for outra.

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

function Get-GitConfigValue {
    param([Parameter(Mandatory)] [string] $Key)
    Push-Location $script:GithubActorProjectRoot
    try {
        $out = & git config --get $Key 2>$null
        if ($LASTEXITCODE -ne 0) { return $null }
        return ("$out").Trim()
    }
    finally {
        Pop-Location
    }
}

function Test-UnderPersonalProjects {
    $full = [System.IO.Path]::GetFullPath($script:GithubActorProjectRoot).TrimEnd('\')
    $pessoais = [System.IO.Path]::GetFullPath((Join-Path $env:USERPROFILE 'Desenvolvimento\projetos\pessoais')).TrimEnd('\')
    return $full.Equals($pessoais, [System.StringComparison]::OrdinalIgnoreCase) -or
        $full.StartsWith($pessoais + '\', [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-GithubActor {
    if ($script:GithubActor) { return $script:GithubActor }

    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "gh nao encontrado no PATH. Instale a GitHub CLI e entre com a conta pessoal. Veja docs/IDENTIDADE_GITHUB.md."
    }
    if (-not (Test-UnderPersonalProjects)) {
        throw "O publish deste repositorio so roda em Desenvolvimento\projetos\pessoais, com o e-mail dionerdfrg3@gmail.com. Veja docs/IDENTIDADE_GITHUB.md."
    }

    $email = Get-GitConfigValue 'user.email'
    $name = (Get-GitConfigValue 'user.name')
    $login = Get-GitConfigValue 'credential.https://github.com.username'
    if ($email -ne 'dionerdfrg3@gmail.com') {
        throw "Repositorio pessoal com user.email '$email'. O commit so pode sair como dionerdfrg3@gmail.com. Veja docs/IDENTIDADE_GITHUB.md."
    }
    $name = ($name -replace '[\r\n"]', ' ').Trim()
    if ([string]::IsNullOrWhiteSpace($name)) {
        throw "user.name ausente para dionerdfrg3@gmail.com. Veja docs/IDENTIDADE_GITHUB.md."
    }
    if ($login -notmatch '^[A-Za-z0-9-]+$') {
        throw "credential.https://github.com.username ausente ou invalido ('$login'). A conta do publish pessoal e a desse usuario. Veja docs/IDENTIDADE_GITHUB.md."
    }

    $origin = Get-GithubOwnerRepo -ProjectRoot $script:GithubActorProjectRoot
    $repo = "$($origin.Owner)/$($origin.Repo)"

    $forcedToken = [bool]($env:GH_TOKEN -or $env:GITHUB_TOKEN)
    $active = Get-GhAccount -Repo $repo
    if (-not $active) {
        throw "Nao foi possivel ler a conta do gh. Rode 'gh auth status'. Veja docs/IDENTIDADE_GITHUB.md."
    }

    $account = $null
    $note = $null
    if ($active.Login -eq $login) {
        if (-not $active.CanPush) {
            throw "A conta $login (dionerdfrg3@gmail.com) nao tem escrita em $repo. O publish nao usa outra conta."
        }
        $account = $active
    }
    else {
        if ($forcedToken) {
            throw "GH_TOKEN/GITHUB_TOKEN e a conta $($active.Login). Em projetos pessoais o publish so usa $login (dionerdfrg3@gmail.com). Apague a variavel nesta sessao. Veja docs/IDENTIDADE_GITHUB.md."
        }
        $token = Invoke-GhQuiet -GhArgs @('auth', 'token', '--hostname', 'github.com', '--user', $login)
        if (-not $token) {
            throw "A conta $login (dionerdfrg3@gmail.com) nao esta logada no gh. A conta ativa e $($active.Login), e o publish nao usa essa conta. Rode 'gh auth login'. Veja docs/IDENTIDADE_GITHUB.md."
        }
        $account = Get-GhAccount -Repo $repo -Token $token
        if (-not $account -or $account.Login -ne $login) {
            throw "Nao foi possivel ler a conta $login no gh. Veja docs/IDENTIDADE_GITHUB.md."
        }
        if (-not $account.CanPush) {
            throw "A conta $login (dionerdfrg3@gmail.com) nao tem escrita em $repo. O publish nao usa $($active.Login)."
        }
        $note = "A conta ativa do gh e $($active.Login). O commit sai como $name <dionerdfrg3@gmail.com> ($login)."
    }

    if ($account.Id -notmatch '^\d+$') {
        throw "Id da conta gh ausente para '$login'."
    }

    # Fora do objeto para nao aparecer se alguem imprimir o ator.
    $script:GithubActorToken = $account.Token
    $script:GithubActor = [pscustomobject]@{
        Login = $login
        Name  = $name
        Id    = $account.Id
        Email = 'dionerdfrg3@gmail.com'
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

        # -c grava o e-mail da pasta pessoal. O helper usa o token dessa conta, nao o da conta ativa.
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
