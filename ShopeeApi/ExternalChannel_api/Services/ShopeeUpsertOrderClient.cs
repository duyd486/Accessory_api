using System.Net.Http.Json;
using ExternalChannel_api.Models;

namespace ExternalChannel_api.Services;

public sealed class ShopeeUpsertOrderClient
{
    private const string UpsertUrl = "http://localhost:8000/api/shopee/upsert-order";
    private readonly HttpClient _httpClient;

    public ShopeeUpsertOrderClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<HttpResponseMessage> UpsertAsync(UpsertShopeeOrderRequest request, CancellationToken cancellationToken)
    {
        return _httpClient.PostAsJsonAsync(UpsertUrl, request, cancellationToken);
    }
}
