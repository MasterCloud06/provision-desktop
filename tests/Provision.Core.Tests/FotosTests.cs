using System.Net;
using Provision.Core.Api;

namespace Provision.Core.Tests;

public sealed class FotosTests
{
    [Fact]
    public async Task Photo_requests_resolve_against_api_and_send_staff_token()
    {
        var handler = new Captura();
        using var http = new ApiClient("http://localhost:5080/api/optica/demo/v1", handler) { Token = "staff-token" };
        var bytes = await http.DescargarFotoAsync("imagenes/1234?v=abc", TestContext.Current.CancellationToken);
        Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
        Assert.Equal("http://localhost:5080/api/optica/demo/v1/imagenes/1234?v=abc", handler.Url);
        Assert.Equal("Bearer staff-token", handler.Token);
    }

    [Theory]
    [InlineData("https://external.test/image.jpg")]
    [InlineData("//external.test/image.jpg")]
    [InlineData("imagenes/../auth/me")]
    public async Task Photo_download_never_sends_credentials_to_external_hosts(string ruta)
    {
        var handler = new Captura();
        using var http = new ApiClient("http://localhost:5080/api/optica/demo/v1", handler) { Token = "staff-token" };
        await Assert.ThrowsAsync<ArgumentException>(() => http.DescargarFotoAsync(ruta, TestContext.Current.CancellationToken));
        Assert.Null(handler.Url);
    }

    private sealed class Captura : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? Token { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri!.ToString();
            Token = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        }
    }
}
