#Requires -Version 5.1
# Cria certificado autoassinado para assinatura de codigo (Code Signing) se nao existir.
# Para que o UAC exiba "Dioner Frigi" como fornecedor, o certificado precisa estar
# em Trusted Root e Trusted Publishers da maquina local (requer admin).

$subject = 'CN=Dioner Frigi'
$existing = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.Subject -eq $subject }

if ($existing) {
    $cert = $existing | Select-Object -First 1
    Write-Host "Certificado ja existe: $($cert.Thumbprint)" -ForegroundColor Green
} else {
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $subject `
        -CertStoreLocation Cert:\CurrentUser\My `
        -NotAfter (Get-Date).AddYears(5) `
        -FriendlyName 'BananaSuisa Code Signing'

    Write-Host "Certificado criado: $($cert.Thumbprint)" -ForegroundColor Green
}

Write-Host "Sujeito: $($cert.Subject)" -ForegroundColor Cyan
Write-Host "Validade: $($cert.NotAfter)" -ForegroundColor Cyan

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host ''
    Write-Warning 'Para que o Windows confie neste certificado e exiba "Dioner Frigi" no UAC,'
    Write-Warning 'execute este script como Administrador. Ele instalara o certificado em'
    Write-Warning 'Trusted Root e Trusted Publishers da maquina local.'
    exit 0
}

$rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root', 'LocalMachine')
$rootStore.Open('ReadWrite')
$alreadyInRoot = $rootStore.Certificates | Where-Object { $_.Thumbprint -eq $cert.Thumbprint }
if (-not $alreadyInRoot) {
    $rootStore.Add($cert)
    Write-Host 'Certificado adicionado a Trusted Root Certification Authorities (LocalMachine).' -ForegroundColor Green
} else {
    Write-Host 'Ja esta em Trusted Root.' -ForegroundColor DarkGray
}
$rootStore.Close()

$pubStore = New-Object System.Security.Cryptography.X509Certificates.X509Store('TrustedPublisher', 'LocalMachine')
$pubStore.Open('ReadWrite')
$alreadyInPub = $pubStore.Certificates | Where-Object { $_.Thumbprint -eq $cert.Thumbprint }
if (-not $alreadyInPub) {
    $pubStore.Add($cert)
    Write-Host 'Certificado adicionado a Trusted Publishers (LocalMachine).' -ForegroundColor Green
} else {
    Write-Host 'Ja esta em Trusted Publishers.' -ForegroundColor DarkGray
}
$pubStore.Close()

Write-Host ''
Write-Host 'Pronto! Ao publicar com ".\bs.cmd publish", o .exe sera assinado e o UAC' -ForegroundColor Green
Write-Host 'exibira "Dioner Frigi" como fornecedor verificado.' -ForegroundColor Green
