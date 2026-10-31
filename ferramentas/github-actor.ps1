#Requires -Version 5.1
# Commit e release saem na conta do gh com escrita que a pessoa escolhe no dialogo.
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

function Show-GhAccountChoice {
    param(
        [Parameter(Mandatory)] [object[]] $Accounts,
        [Parameter(Mandatory)] [string] $Repo,
        [string] $PreferredLogin
    )

    $labels = @(
        foreach ($account in $Accounts) {
            $profile = Get-ProfileName -Account $account
            if ($profile -eq $account.Login) { $account.Login } else { "$profile ($($account.Login))" }
        }
    )

    $ui = @'
param($Title, $Message, [string[]] $Labels, [int] $Selected)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$form = New-Object System.Windows.Forms.Form
$form.Text = $Title
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen
$form.MinimizeBox = $false
$form.MaximizeBox = $false
$form.ShowInTaskbar = $false
$form.TopMost = $true
$form.Font = New-Object System.Drawing.Font("Segoe UI", 9)
$listHeight = [Math]::Min(180, [Math]::Max(48, 8 + (20 * $Labels.Count)))
$form.ClientSize = New-Object System.Drawing.Size(440, (128 + $listHeight))
$label = New-Object System.Windows.Forms.Label
$label.Text = $Message
$label.Location = New-Object System.Drawing.Point(12, 12)
$label.Size = New-Object System.Drawing.Size(416, 48)
$list = New-Object System.Windows.Forms.ListBox
$list.Location = New-Object System.Drawing.Point(12, 68)
$list.Size = New-Object System.Drawing.Size(416, $listHeight)
$list.IntegralHeight = $false
foreach ($item in $Labels) { [void]$list.Items.Add($item) }
if ($list.Items.Count -gt 0) {
    $index = 0
    if ($Selected -ge 0 -and $Selected -lt $list.Items.Count) { $index = $Selected }
    $list.SelectedIndex = $index
}
$ok = New-Object System.Windows.Forms.Button
$ok.Text = "OK"
$ok.DialogResult = [System.Windows.Forms.DialogResult]::OK
$ok.Location = New-Object System.Drawing.Point(248, (80 + $listHeight))
$ok.Size = New-Object System.Drawing.Size(88, 28)
$cancel = New-Object System.Windows.Forms.Button
$cancel.Text = "Cancelar"
$cancel.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
$cancel.Location = New-Object System.Drawing.Point(340, (80 + $listHeight))
$cancel.Size = New-Object System.Drawing.Size(88, 28)
$form.Controls.AddRange(@($label, $list, $ok, $cancel))
$form.AcceptButton = $ok
$form.CancelButton = $cancel
$form.Add_Shown({ $form.Activate() })
$result = $form.ShowDialog()
$index = -1
if ($result -eq [System.Windows.Forms.DialogResult]::OK) { $index = $list.SelectedIndex }
$form.Dispose()
return $index
'@

    $rs = [runspacefactory]::CreateRunspace()
    $rs.ApartmentState = 'STA'
    $rs.ThreadOptions = 'ReuseThread'
    $rs.Open()
    $ps = $null
    try {
        $ps = [powershell]::Create()
        $ps.Runspace = $rs
        $message = "Escolha a conta do GitHub que deve assinar este commit em $Repo."
        $preferred = 0
        for ($i = 0; $i -lt $Accounts.Count; $i++) {
            if ($Accounts[$i].Login -eq $PreferredLogin) { $preferred = $i; break }
        }
        [void]$ps.AddScript($ui).AddArgument('Ribanense Solucoes').AddArgument($message).AddArgument([string[]]$labels).AddArgument($preferred)
        $picked = @($ps.Invoke())
        if ($ps.HadErrors) {
            $detail = ($ps.Streams.Error | ForEach-Object { $_.ToString() }) -join ' '
            throw "Nao foi possivel abrir o dialogo de selecao de conta. $detail"
        }
    }
    finally {
        if ($ps) { $ps.Dispose() }
        $rs.Close()
        $rs.Dispose()
    }

    if ($picked.Count -eq 0 -or [int]$picked[-1] -lt 0) { return $null }
    return $Accounts[[int]$picked[-1]]
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

    $chosen = Show-GhAccountChoice -Accounts @($candidates) -Repo $Repo -PreferredLogin $active.Login
    if (-not $chosen) {
        throw "Selecao de conta cancelada."
    }
    $note = $null
    if ($chosen.Login -ne $active.Login) {
        $note = "A conta ativa do gh e $($active.Login). O publish usa $($chosen.Login), escolhida no dialogo."
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
