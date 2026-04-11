using BananaSuisa.Core.Winget;

namespace BananaSuisa.Services.Tests;

public sealed class WingetInstallationOriginTests
{
    [Theory]
    [InlineData("", "MSIX\\Microsoft.WindowsStore_22602.1401.6.0_x64__8wekyb3d8bbwe", "Microsoft Store / MSIX")]
    [InlineData("", "APPX\\Some.Legacy.App_1.0.0.0_x64__8wekyb3d8bbwe", "Microsoft Store / MSIX")]
    [InlineData("msstore", "9NBLGGH4NNS1", "Microsoft Store (UWP/MSIX)")]
    [InlineData("Microsoft Store", "Some.App", "Microsoft Store (UWP/MSIX)")]
    [InlineData("Loja do Windows", "Some.App", "Microsoft Store (UWP/MSIX)")]
    [InlineData("winget", "7zip.7zip", "Winget (repositório)")]
    [InlineData("", "9NBLGGH4NNS1", "Microsoft Store (catálogo)")]
    [InlineData("", "ARP\\Machine\\X64\\Git_is1", "Externo (ARP / fora do catálogo)")]
    public void Resolve_CasosRepresentativos(string source, string id, string expected)
    {
        string r = WingetInstallationOrigin.Resolve(source, id);
        Assert.Equal(expected, r);
    }

    [Theory]
    [InlineData("winget", "7zip.7zip", true)]
    [InlineData("msstore", "9NBLGGH4NNS1", true)]
    [InlineData("Microsoft Store", "Some.App", true)]
    [InlineData("CustomVendor", "Foo.Bar", false)]
    [InlineData("", "ARP\\Machine\\X64\\Git_is1", false)]
    [InlineData("", "MSIX\\Microsoft.WindowsStore_1.0.0.0_x64__8wekyb3d8bbwe", true)]
    public void IsEligibleForInstallTab_CasosRepresentativos(string source, string id, bool expected)
    {
        string origin = WingetInstallationOrigin.Resolve(source, id);
        bool ok = WingetInstallationOrigin.IsEligibleForInstallTab(source, id, origin);
        Assert.Equal(expected, ok);
    }
}
