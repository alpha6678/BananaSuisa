#Requires -Version 5.1
# Identidade de publicacao: a conta ativa do gh, nao o user.email da maquina.

function Get-GithubActor {
    if ($script:GithubActor) { return $script:GithubActor }

    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "gh nao encontrado no PATH. Instale a GitHub CLI e entre com a conta que deve assinar o publish. Veja docs/IDENTIDADE_GITHUB.md."
    }

    $raw = & gh api user --jq '{login:.login,name:.name,id:.id}'
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($raw | Out-String))) {
        throw "Nao foi possivel ler a conta ativa do gh. Rode 'gh auth status' e entre com a sua conta ('gh auth login' ou 'gh auth switch'). Veja docs/IDENTIDADE_GITHUB.md."
    }

    $user = ($raw | Out-String).Trim() | ConvertFrom-Json
    $login = [string] $user.login
    if ($login -notmatch '^[A-Za-z0-9-]+$') {
        throw "Login do gh invalido para assinar o commit: '$login'."
    }

    $id = [string] $user.id
    if ($id -notmatch '^\d+$') {
        throw "Id da conta gh ausente para '$login'."
    }

    $name = [string] $user.name
    $name = ($name -replace '[\r\n"]', ' ').Trim()
    if ([string]::IsNullOrWhiteSpace($name)) { $name = $login }

    $script:GithubActor = [pscustomobject]@{
        Login = $login
        Name  = $name
        Id    = $id
        Email = "$id+$login@users.noreply.github.com"
    }
    return $script:GithubActor
}

function Invoke-GitAsActor {
    param(
        [Parameter(Mandatory)] [string[]] $GitArgs,
        [string] $FailureMessage = 'Comando git da publicacao falhou.'
    )

    $actor = Get-GithubActor
    $keys = @('GIT_AUTHOR_NAME', 'GIT_AUTHOR_EMAIL', 'GIT_COMMITTER_NAME', 'GIT_COMMITTER_EMAIL')
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

        # -c vence o user.email e o credential.username gravados na maquina.
        # helper vazio zera helpers de prioridade menor; o seguinte usa o token do gh ativo.
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
    return $code
}
