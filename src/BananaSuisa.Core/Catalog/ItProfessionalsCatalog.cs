using System.Collections.Generic;

namespace BananaSuisa.Core.Catalog;

public static class ItProfessionalsCatalog
{
    public static IReadOnlyList<CatalogItem> GetRecommendations()
    {
        return new List<CatalogItem>
        {
            // Desenvolvimento & Editores
            new CatalogItem("Visual Studio Code", "Microsoft.VisualStudioCode", "Desenvolvimento", true, "Curadoria TI"),
            new CatalogItem("Git", "Git.Git", "Desenvolvimento", true, "Curadoria TI"),
            new CatalogItem("Postman", "Postman.Postman", "Desenvolvimento", true, "Curadoria TI"),
            new CatalogItem("Insomnia", "Insomnia.Insomnia", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("Node.js (LTS)", "OpenJS.NodeJS.LTS", "Desenvolvimento", true, "Curadoria TI"),
            new CatalogItem("Node.js", "OpenJS.NodeJS", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("NVM for Windows", "CoreyButler.NVMforWindows", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("Python 3.12", "Python.Python.3.12", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("Python 3.11", "Python.Python.3.11", "Desenvolvimento", true, "Curadoria TI"),
            new CatalogItem("Python 3.10", "Python.Python.3.10", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem(".NET SDK 8.0", "Microsoft.DotNet.SDK.8", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem(".NET SDK 7.0", "Microsoft.DotNet.SDK.7", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem(".NET SDK 6.0", "Microsoft.DotNet.SDK.6", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("Rust (MSVC)", "Rustlang.Rust.MSVC", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("Rustup", "Rustlang.Rustup", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("IntelliJ IDEA Community", "JetBrains.IntelliJIDEA.Community", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("PyCharm Community", "JetBrains.PyCharm.Community", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("JetBrains Toolbox", "JetBrains.Toolbox", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("Visual Studio Community 2022", "Microsoft.VisualStudio.2022.Community", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("R for Windows", "RProject.R", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("GitHub CLI", "GitHub.cli", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("Sourcetree", "Atlassian.Sourcetree", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("Fork", "Fork.Fork", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("Sublime Text 4", "SublimeHQ.SublimeText.4", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("Geany", "Geany.Geany", "Desenvolvimento", false, "Curadoria TI"),
            new CatalogItem("Vim", "vim.vim", "Desenvolvimento", false, "Curadoria TI"),

            // Terminais & Produtividade CLI
            new CatalogItem("Windows Terminal", "Microsoft.WindowsTerminal", "Terminais", true, "Curadoria TI"),
            new CatalogItem("PowerShell", "Microsoft.PowerShell", "Terminais", true, "Curadoria TI"),
            new CatalogItem("Starship", "Starship.Starship", "Terminais", false, "Curadoria TI"),
            new CatalogItem("jq", "jqlang.jq", "Utilitários CLI", false, "Curadoria TI"),
            new CatalogItem("bat", "sharkdp.bat", "Utilitários CLI", false, "Curadoria TI"),
            
            // Containers & Infraestrutura
            new CatalogItem("Docker Desktop", "Docker.DockerDesktop", "Infraestrutura", true, "Curadoria TI"),
            new CatalogItem("Docker CLI", "Docker.DockerCLI", "Infraestrutura", false, "Curadoria TI"),
            new CatalogItem("Kubernetes CLI (kubectl)", "Kubernetes.kubectl", "Infraestrutura", false, "Curadoria TI"),
            new CatalogItem("Helm", "Helm.Helm", "Infraestrutura", false, "Curadoria TI"),
            new CatalogItem("HashiCorp Terraform", "Hashicorp.Terraform", "Infraestrutura", false, "Curadoria TI"),
            new CatalogItem("HashiCorp Packer", "Hashicorp.Packer", "Infraestrutura", false, "Curadoria TI"),
            new CatalogItem("Pulumi", "Pulumi.Pulumi", "Infraestrutura", false, "Curadoria TI"),
            new CatalogItem("Bicep CLI", "Microsoft.Bicep", "Infraestrutura", false, "Curadoria TI"),
            new CatalogItem("Multipass", "Canonical.Multipass", "Infraestrutura", false, "Curadoria TI"),
            new CatalogItem("Ubuntu 22.04 LTS (WSL)", "Canonical.Ubuntu.2204", "Infraestrutura", false, "Curadoria TI"),
            new CatalogItem("Windows Subsystem for Linux", "Microsoft.WSL", "Infraestrutura", true, "Curadoria TI"),

            // Cloud SDKs
            new CatalogItem("Azure CLI", "Microsoft.AzureCLI", "Cloud", false, "Curadoria TI"),
            new CatalogItem("AWS CLI", "Amazon.AWSCLI", "Cloud", false, "Curadoria TI"),
            new CatalogItem("Google Cloud SDK", "Google.CloudSDK", "Cloud", false, "Curadoria TI"),

            // Banco de Dados
            new CatalogItem("DB Browser for SQLite", "DBBrowserForSQLite.DBBrowserForSQLite", "Banco de Dados", false, "Curadoria TI"),
            new CatalogItem("HeidiSQL", "HeidiSQL.HeidiSQL", "Banco de Dados", false, "Curadoria TI"),
            new CatalogItem("TablePlus", "TablePlus.TablePlus", "Banco de Dados", false, "Curadoria TI"),
            new CatalogItem("SQL Server Management Studio", "Microsoft.SQLServerManagementStudio", "Banco de Dados", false, "Curadoria TI"),

            // Utilitários do Sistema & Ferramentas
            new CatalogItem("PowerToys", "Microsoft.PowerToys", "Sistema", true, "Curadoria TI"),
            new CatalogItem("gsudo", "gerardog.gsudo", "Sistema", false, "Curadoria TI"),
            new CatalogItem("7-Zip", "7zip.7zip", "Sistema", true, "Curadoria TI"),
            new CatalogItem("PeaZip", "Giorgiotani.Peazip", "Sistema", false, "Curadoria TI"),
            new CatalogItem("Notepad++", "Notepad++.Notepad++", "Utilitários", true, "Curadoria TI"),
            new CatalogItem("PuTTY", "PuTTY.PuTTY", "Rede", true, "Curadoria TI"),
            new CatalogItem("WinDirStat", "WinDirStat.WinDirStat", "Sistema", false, "Curadoria TI"),
            new CatalogItem("Oracle VirtualBox", "Oracle.VirtualBox", "Infraestrutura", false, "Curadoria TI"),
            new CatalogItem("Everything", "voidtools.Everything", "Utilitários", true, "Curadoria TI"),
            new CatalogItem("Process Explorer", "Microsoft.Sysinternals.ProcessExplorer", "Sistema", true, "Curadoria TI"),
            new CatalogItem("Autoruns", "Microsoft.Sysinternals.Autoruns", "Sistema", true, "Curadoria TI"),
            new CatalogItem("TCPView", "Microsoft.Sysinternals.TCPView", "Sistema", false, "Curadoria TI"),
            new CatalogItem("WinMerge", "WinMerge.WinMerge", "Utilitários", false, "Curadoria TI"),
            new CatalogItem("WinSCP", "WinSCP.WinSCP", "Rede", false, "Curadoria TI"),
            new CatalogItem("WinRAR", "RARLab.WinRAR", "Sistema", false, "Curadoria TI"),
            new CatalogItem("Bandizip", "Bandisoft.Bandizip", "Sistema", false, "Curadoria TI"),
            new CatalogItem("Rufus", "Rufus.Rufus", "Sistema", false, "Curadoria TI"),
            new CatalogItem("balenaEtcher", "Balena.Etcher", "Sistema", false, "Curadoria TI"),
            new CatalogItem("Syncthing", "Syncthing.Syncthing", "Utilitários", false, "Curadoria TI"),
            new CatalogItem("ShareX", "ShareX.ShareX", "Utilitários", false, "Curadoria TI"),
            new CatalogItem("Flow Launcher", "Flow-Launcher.Flow-Launcher", "Utilitários", false, "Curadoria TI"),

            // Redes, Diagnóstico de Rede & Segurança
            new CatalogItem("Tailscale", "Tailscale.Tailscale", "Rede", false, "Curadoria TI"),
            new CatalogItem("Cloudflare WARP", "Cloudflare.Warp", "Rede", false, "Curadoria TI"),
            new CatalogItem("WireGuard", "WireGuard.WireGuard", "Rede", false, "Curadoria TI"),
            new CatalogItem("Wireshark", "WiresharkFoundation.Wireshark", "Rede", true, "Curadoria TI"),
            new CatalogItem("Nmap", "Insecure.Nmap", "Rede", false, "Curadoria TI"),
            new CatalogItem("Angry IP Scanner", "angryziber.AngryIPScanner", "Rede", false, "Curadoria TI"),
            new CatalogItem("Advanced IP Scanner", "Famatech.AdvancedIPScanner", "Rede", false, "Curadoria TI"),
            new CatalogItem("Fiddler Classic", "Telerik.Fiddler.Classic", "Rede", false, "Curadoria TI"),
            new CatalogItem("KeePassXC", "KeePassXCTeam.KeePassXC", "Segurança", true, "Curadoria TI"),
            new CatalogItem("Bitwarden", "Bitwarden.Bitwarden", "Segurança", true, "Curadoria TI"),

            // Navegadores
            new CatalogItem("Google Chrome", "Google.Chrome", "Navegadores", true, "Curadoria TI"),
            new CatalogItem("Mozilla Firefox", "Mozilla.Firefox", "Navegadores", true, "Curadoria TI"),
            new CatalogItem("Brave", "Brave.Brave", "Navegadores", false, "Curadoria TI"),
            new CatalogItem("Microsoft Edge", "Microsoft.Edge", "Navegadores", false, "Curadoria TI"),
            new CatalogItem("Opera", "Opera.Opera", "Navegadores", false, "Curadoria TI"),
            new CatalogItem("Opera GX", "Opera.OperaGX", "Navegadores", false, "Curadoria TI"),
            new CatalogItem("Vivaldi", "Vivaldi.Vivaldi", "Navegadores", false, "Curadoria TI"),
            new CatalogItem("Tor Browser", "TorProject.TorBrowser", "Navegadores", false, "Curadoria TI"),

            // Gestão do Conhecimento & Produtividade
            new CatalogItem("Obsidian", "Obsidian.Obsidian", "Produtividade", true, "Curadoria TI"),
            new CatalogItem("Notion", "Notion.Notion", "Produtividade", false, "Curadoria TI"),
            new CatalogItem("Logseq", "Logseq.Logseq", "Produtividade", false, "Curadoria TI"),
            new CatalogItem("PowerBI Desktop", "Microsoft.PowerBI", "Produtividade", false, "Curadoria TI"),
            new CatalogItem("LibreOffice", "TheDocumentFoundation.LibreOffice", "Produtividade", false, "Curadoria TI"),
            new CatalogItem("WPS Office", "Kingsoft.WPSOffice", "Produtividade", false, "Curadoria TI"),

            // Comunicação & Acesso Remoto
            new CatalogItem("Discord", "Discord.Discord", "Comunicação", false, "Curadoria TI"),
            new CatalogItem("Microsoft Teams", "Microsoft.Teams", "Comunicação", true, "Curadoria TI"),
            new CatalogItem("Zoom", "Zoom.Zoom", "Comunicação", false, "Curadoria TI"),
            new CatalogItem("Mozilla Thunderbird", "Mozilla.Thunderbird", "Comunicação", false, "Curadoria TI"),
            new CatalogItem("AnyDesk", "AnyDesk.AnyDesk", "Acesso Remoto", false, "Curadoria TI"),
            new CatalogItem("TeamViewer", "TeamViewer.TeamViewer", "Acesso Remoto", false, "Curadoria TI"),
            new CatalogItem("RealVNC Viewer", "RealVNC.VNCViewer", "Acesso Remoto", false, "Curadoria TI"),
            new CatalogItem("Remote Desktop Manager", "Devolutions.RemoteDesktopManager", "Acesso Remoto", false, "Curadoria TI"),
            new CatalogItem("mRemoteNG", "mRemoteNG.mRemoteNG", "Acesso Remoto", false, "Curadoria TI"),

            // Design & Mídia
            new CatalogItem("Figma", "Figma.Figma", "Design", false, "Curadoria TI"),
            new CatalogItem("GIMP 3", "GIMP.GIMP.3", "Design", false, "Curadoria TI"),
            new CatalogItem("Krita", "KDE.Krita", "Design", false, "Curadoria TI"),
            new CatalogItem("Inkscape", "Inkscape.Inkscape", "Design", false, "Curadoria TI"),
            new CatalogItem("Upscayl", "Upscayl.Upscayl", "Design", false, "Curadoria TI"),
            new CatalogItem("VLC media player", "VideoLAN.VLC", "Mídia", true, "Curadoria TI"),
            new CatalogItem("OBS Studio", "OBSProject.OBSStudio", "Mídia", false, "Curadoria TI"),
            new CatalogItem("Audacity", "Audacity.Audacity", "Mídia", false, "Curadoria TI"),
            new CatalogItem("Spotify", "Spotify.Spotify", "Mídia", false, "Curadoria TI"),

            // Captura de Tela & Gravação
            new CatalogItem("Flameshot", "Flameshot.Flameshot", "Utilitários", false, "Curadoria TI"),
            new CatalogItem("Lightshot", "Skillbrains.Lightshot", "Utilitários", false, "Curadoria TI"),
            new CatalogItem("Greenshot", "Greenshot.Greenshot", "Utilitários", false, "Curadoria TI"),
            new CatalogItem("ScreenToGif", "NickeManarin.ScreenToGif", "Utilitários", false, "Curadoria TI"),

            // Editores de Código & IDE
            new CatalogItem("Cursor", "Anysphere.Cursor", "Desenvolvimento", false, "Curadoria TI"),

            // PDF & Documentos
            new CatalogItem("PDFCreator Free", "Avanquestpdfforge.PDFCreator-Free", "Utilitários", false, "Curadoria TI"),
            new CatalogItem("SumatraPDF", "SumatraPDF.SumatraPDF", "Utilitários", false, "Curadoria TI"),
            new CatalogItem("Foxit PDF Editor", "Foxit.PhantomPDF", "Utilitários", false, "Curadoria TI"),
            new CatalogItem("wkhtmltopdf", "wkhtmltopdf.wkhtmltox", "Utilitários", false, "Curadoria TI"),

            // Produtividade Desktop
            new CatalogItem("Ditto Clipboard", "Ditto.Ditto", "Utilitários", false, "Curadoria TI"),
            new CatalogItem("draw.io", "JGraph.Draw", "Produtividade", false, "Curadoria TI"),

            // Mídia & Conversão
            new CatalogItem("HandBrake", "HandBrake.HandBrake", "Mídia", false, "Curadoria TI"),
            new CatalogItem("qBittorrent", "qBittorrent.qBittorrent", "Utilitários", false, "Curadoria TI"),

            // Diagnóstico & Benchmark
            new CatalogItem("CrystalDiskInfo", "CrystalDewWorld.CrystalDiskInfo", "Sistema", false, "Curadoria TI"),
            new CatalogItem("CrystalMark 3D25", "CrystalDewWorld.CrystalMark3D25", "Sistema", false, "Curadoria TI"),
            new CatalogItem("MiniTool Partition Wizard", "MiniTool.PartitionWizard.Free", "Sistema", false, "Curadoria TI"),
            new CatalogItem("Revo Uninstaller", "RevoUninstaller.RevoUninstaller", "Sistema", false, "Curadoria TI"),
            new CatalogItem("TreeSize Free", "JAMSoftware.TreeSize.Free", "Sistema", false, "Curadoria TI"),
            new CatalogItem("Ventoy", "Ventoy.Ventoy", "Sistema", false, "Curadoria TI"),

            // Hardware & Diagnóstico
            new CatalogItem("HWiNFO", "REALiX.HWiNFO", "Diagnóstico", false, "Curadoria TI"),
            new CatalogItem("CPU-Z", "CPUID.CPU-Z", "Diagnóstico", false, "Curadoria TI"),
            new CatalogItem("GPU-Z", "TechPowerUp.GPU-Z", "Diagnóstico", false, "Curadoria TI"),
            new CatalogItem("HWMonitor", "CPUID.HWMonitor", "Diagnóstico", false, "Curadoria TI"),

            // Emuladores
            new CatalogItem("Dolphin Emulator", "DolphinEmulator.Dolphin", "Mídia", false, "Curadoria TI")
        };
    }
}
