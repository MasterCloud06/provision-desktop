using System.Security.Cryptography;
using System.Text;
using Provision.Core.Actualizaciones;

namespace Provision.Core.Tests;

public sealed class OrigenSeguroTests
{
    private const string Repo = "provision-desktop";

    [Theory]
    [InlineData("https://github.com/MasterCloud06/provision-desktop/releases/download/escritorio-7/Provision.exe", true)]
    [InlineData("http://github.com/MasterCloud06/provision-desktop/releases/download/escritorio-7/Provision.exe", false)]
    [InlineData("https://github.com/otro/provision-desktop/releases/download/escritorio-7/Provision.exe", false)]
    [InlineData("https://github.com/MasterCloud06/provision-app/releases/download/v0.1.0-9/provision.apk", false)]
    [InlineData("https://github.com.evil.example/MasterCloud06/provision-desktop/releases/download/x/Provision.exe", false)]
    [InlineData("https://evil.example/MasterCloud06/provision-desktop/releases/download/x/Provision.exe", false)]
    [InlineData("https://github.com/MasterCloud06/provision-desktop/archive/main.zip", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Solo_acepta_releases_del_repositorio_por_https(string? url, bool permitida) =>
        Assert.Equal(permitida, OrigenSeguro.EsUrlPermitida(url, Repo));

    [Theory]
    [InlineData("https://objects.githubusercontent.com/github-production-release-asset/x", true)]
    [InlineData("https://release-assets.githubusercontent.com/github-production-release-asset/x", true)]
    [InlineData("https://github.com/MasterCloud06/provision-desktop/releases/download/x/Provision.exe", true)]
    [InlineData("http://objects.githubusercontent.com/x", false)]
    [InlineData("https://githubusercontent.com.evil.example/x", false)]
    public void El_destino_tras_redirecciones_debe_seguir_en_github(string url, bool permitido) =>
        Assert.Equal(permitido, OrigenSeguro.EsDestinoPermitido(new Uri(url)));

    [Fact]
    public void El_hash_publicado_debe_coincidir_sin_importar_mayusculas()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("Provision.exe"));
        var hex = Convert.ToHexString(hash);

        Assert.True(OrigenSeguro.HashAceptable(hash, hex.ToLowerInvariant()));
        Assert.True(OrigenSeguro.HashAceptable(hash, $" {hex} "));
        Assert.False(OrigenSeguro.HashAceptable(hash, new string('0', 64)));
    }

    [Fact]
    public void Sin_hash_publicado_se_acepta_por_compatibilidad_con_el_backend_antiguo() =>
        Assert.True(OrigenSeguro.HashAceptable(new byte[32], null));
}
