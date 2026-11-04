using System.Net;
using System.Text;
namespace MemeTokenHub.TokenService.IntegrationTests;

internal sealed class HubSourceHandler : HttpMessageHandler
{
    public string Url { get; set; } = "https://x.com/original";
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    public bool EmptyFeed { get; set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(StatusCode) { Content = new StringContent(EmptyFeed ? "[]" : "[{\"tokenAddress\":\"contract\",\"chainId\":\"solana\",\"links\":[{\"type\":\"twitter\",\"url\":\"" + Url + "\"}]}]", Encoding.UTF8, "application/json") });
}
