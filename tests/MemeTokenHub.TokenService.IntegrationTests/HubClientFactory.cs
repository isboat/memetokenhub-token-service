namespace MemeTokenHub.TokenService.IntegrationTests;

internal sealed class HubClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
