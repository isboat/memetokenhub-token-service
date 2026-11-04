using System.Net;
using System.Text;
namespace MemeTokenHub.TokenService.IntegrationTests;

internal sealed class HubSourceHandler : HttpMessageHandler
{
    public string Url { get; set; } = "https://x.com/original";
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[{\"tokenAddress\":\"contract\",\"chainId\":\"solana\",\"links\":[{\"type\":\"twitter\",\"url\":\"" + Url + "\"}]}]", Encoding.UTF8, "application/json") });
}
