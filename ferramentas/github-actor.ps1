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
    if ($null -ne $script:GhLogins) { return $script:GhLogins }
    $raw = Invoke-GhQuiet -GhArgs @('auth', 'status', '--hostname', 'github.com', '--json', 'hosts')
    if (-not $raw) {
        $script:GhLogins = @()
        return $script:GhLogins
    }
    $status = $raw | ConvertFrom-Json
    $script:GhLogins = @(
        $status.hosts.'github.com' |
            Where-Object { $_.state -eq 'success' } |
            ForEach-Object { [string] $_.login } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -Unique
    )
    return $script:GhLogins
}

# Quem o GitHub confirma com escrita no repositorio, tenha sessao nesta maquina ou nao.
# Tenta o token ativo e, se ele nao puder ler a lista, os das outras contas logadas.
# Devolve $null quando nenhum token le a lista: sem lista, a publicacao segue sem o aviso.
function Get-PushCollaborators {
    param([Parameter(Mandatory)] [string] $Repo)
    if ($script:GhCollaborators) { return $script:GhCollaborators }

    $tokens = @()
    foreach ($login in @(Get-GhLogins)) {
        $t = Invoke-GhQuiet -GhArgs @('auth', 'token', '--hostname', 'github.com', '--user', $login)
        if ($t) { $tokens += $t }
    }
    $tokens += $null

    foreach ($candidateToken in $tokens) {
        $raw = Invoke-GhQuiet -Token $candidateToken -GhArgs @(
            'api', "repos/$Repo/collaborators", '--paginate',
            '--jq', '.[] | select(.permissions.push) | "\(.login) \(.id)"'
        )
        if (-not $raw) { continue }
        $list = @()
        foreach ($line in ($raw -split "`r?`n")) {
            $parts = @(($line.Trim() -split '\s+') | Where-Object { $_ })
            if ($parts.Count -lt 2) { continue }
            if ($parts[1] -notmatch '^\d+$') { continue }
            $list += [pscustomobject]@{ Login = $parts[0]; Id = $parts[1] }
        }
        if ($list.Count -gt 0) {
            $script:GhCollaborators = @($list)
            return $script:GhCollaborators
        }
    }
    return $null
}

# Cada pessoa assina e envia com a propria conta, na propria maquina. Quando alguem com
# escrita no origin nao tem sessao aqui, todo commit desta maquina sai no nome de outra
# pessoa. Ninguem percebe na hora: o commit parece normal e so o GitHub mostra o dono.
function Write-ForeignAuthorsNote {
    param(
        [AllowNull()] [object[]] $Collaborators,
        [Parameter(Mandatory)] [string] $SignerLogin,
        [Parameter(Mandatory)] [string] $Repo
    )
    if (-not $Collaborators) { return }
    $logged = @(Get-GhLogins)
    $missing = @(
        $Collaborators |
            Where-Object { $logged -notcontains $_.Login -and $_.Login -ne $SignerLogin } |
            ForEach-Object { $_.Login } |
            Sort-Object
    )
    if ($missing.Count -eq 0) { return }

    $line1 = "Nesta maquina os commits saem como $SignerLogin."
    $line2 = "Tambem tem escrita em $Repo`: $($missing -join ', ') (sem sessao do gh aqui)."
    $line3 = "Trabalho de outra pessoa vai no commit dela, na maquina dela, com a conta dela."
    foreach ($line in @($line1, $line2, $line3)) {
        if (Get-Command Write-Warn2 -ErrorAction SilentlyContinue) { Write-Warn2 $line }
        else { Write-Host "[!!] $line" -ForegroundColor Yellow }
    }
}

function Get-ProfileName {
    param($Account)
    $name = ($Account.Name -replace '[\r\n"]', ' ').Trim()
    if ([string]::IsNullOrWhiteSpace($name)) { return $Account.Login }
    return $name
}

function Get-AccountEmail {
    param($Account)
    return "$($Account.Id)+$($Account.Login)@users.noreply.github.com"
}

function Read-GhAccountChoice {
    param([Parameter(Mandatory)] [object[]] $Accounts)

    $accounts = @($Accounts)
    Write-Host ""
    for ($i = 0; $i -lt $accounts.Count; $i++) {
        $account = $accounts[$i]
        Write-Host ("  {0}) {1} <{2}>" -f ($i + 1), $account.Login, (Get-AccountEmail -Account $account))
    }

    while ($true) {
        $answer = Read-Host "Numero da conta que assina o commit [1]"
        if ([string]::IsNullOrWhiteSpace($answer)) { return $accounts[0] }
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

    $collaborators = Get-PushCollaborators -Repo $Repo

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
    $candidates = @($candidates | Sort-Object Login)

    $remembered = @($candidates | Where-Object { $_.Login -eq $env:RIBANENSE_GH_LOGIN })
    if ($remembered.Count -eq 1) {
        return [pscustomobject]@{ Account = $remembered[0]; Collaborators = $collaborators }
    }
    if ($candidates.Count -eq 1) {
        return [pscustomobject]@{ Account = $candidates[0]; Collaborators = $collaborators }
    }

    $chosen = Read-GhAccountChoice -Accounts @($candidates)
    $env:RIBANENSE_GH_LOGIN = $chosen.Login
    return [pscustomobject]@{ Account = $chosen; Collaborators = $collaborators }
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
    $email = Get-AccountEmail -Account $account

    if ($account.Login -notmatch '^[A-Za-z0-9-]+$') {
        throw "Login do gh invalido para assinar o commit: '$($account.Login)'."
    }
    if ($account.Id -notmatch '^\d+$') {
        throw "Id da conta gh ausente para '$($account.Login)'."
    }

    Write-ForeignAuthorsNote -Collaborators $selected.Collaborators -SignerLogin $account.Login -Repo $repo

    # Fora do objeto para nao aparecer se alguem imprimir o ator.
    $script:GithubActorToken = $account.Token
    $script:GithubActor = [pscustomobject]@{
        Login = $account.Login
        Name  = $name
        Id    = $account.Id
        Email = $email
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
    $output = @()
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

        # Saida guardada: so aparece quando o comando falha.
        $output = @(& git @prefixed 2>&1 | ForEach-Object {
            if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.ToString() } else { ($_ | Out-String).TrimEnd() }
        })
        $code = $LASTEXITCODE
    }
    finally {
        foreach ($key in $keys) {
            [Environment]::SetEnvironmentVariable($key, $saved[$key], 'Process')
        }
    }

    if ($code -ne 0) {
        if (Get-Command Write-Muted -ErrorAction SilentlyContinue) {
            foreach ($line in $output) {
                foreach ($part in ($line -split "`r?`n")) {
                    if (-not [string]::IsNullOrWhiteSpace($part)) { Write-Muted ("     " + $part.TrimEnd()) }
                }
            }
        }
        throw $FailureMessage
    }
}

function Invoke-GhAsActor {
    param(
        [Parameter(Mandatory)] [string[]] $GhArgs,
        [string] $FailureMessage = 'Comando gh da publicacao falhou.'
    )

    Get-GithubActor | Out-Null
    $saved = [Environment]::GetEnvironmentVariable('GH_TOKEN', 'Process')
    $code = 1
    $output = @()
    try {
        if ($script:GithubActorToken) {
            [Environment]::SetEnvironmentVariable('GH_TOKEN', $script:GithubActorToken, 'Process')
        }
        $output = @(& gh @GhArgs 2>&1 | ForEach-Object {
            if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.ToString() } else { ($_ | Out-String).TrimEnd() }
        })
        $code = $LASTEXITCODE
    }
    finally {
        [Environment]::SetEnvironmentVariable('GH_TOKEN', $saved, 'Process')
    }

    $lines = @(
        foreach ($line in $output) {
            foreach ($part in ($line -split "`r?`n")) {
                if (-not [string]::IsNullOrWhiteSpace($part)) { $part.TrimEnd() }
            }
        }
    )
    if ($code -ne 0) {
        if (Get-Command Write-Muted -ErrorAction SilentlyContinue) {
            foreach ($line in $lines) { Write-Muted ("     " + $line) }
        }
        throw $FailureMessage
    }

    # Sucesso: so a ultima linha (a URL do release) interessa.
    if ($lines.Count -gt 0 -and (Get-Command Write-RbTick -ErrorAction SilentlyContinue)) {
        Write-RbTick ('  ' + $lines[-1]) 'DarkGray'
    }
}
