# AppX / App Installer

O WinGet costuma ser distribuído como pacote **Microsoft.DesktopAppInstaller**. Reparos no BananaSuisa podem usar `Add-AppxPackage`, remoção de pacote antigo e reset de fontes (`winget source reset`), conforme `funcionalidades/actions.ps1`.

Sempre testar em VM com e sem privilégios de administrador conforme o fluxo desejado.
