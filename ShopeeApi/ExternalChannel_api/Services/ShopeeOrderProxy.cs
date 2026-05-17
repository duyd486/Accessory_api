using ExternalChannel_api.Models;

namespace ExternalChannel_api.Services;

public sealed class ShopeeOrderProxy
{
    private readonly HttpClient _httpClient;

    public ShopeeOrderProxy(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<HttpResponseMessage> ForwardCreateAsync(CreateShopeeOrderRequest request, CancellationToken cancellationToken)
    {
        // TODO: thay URL thật của API đích
        var targetUrl = "http://localhost:9000/api/shopee/orders";
        return await _httpClient.PostAsJsonAsync(targetUrl, request, cancellationToken);
    }
}
