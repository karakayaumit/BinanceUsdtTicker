using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Xunit;

namespace BinanceUsdtTicker.Tests;

public class BinanceApiServiceTests
{
    private class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _response;
        public StubHttpMessageHandler(string response) => _response = response;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response)
            };
            return Task.FromResult(message);
        }
    }

    [Fact]
    public async Task GetSymbolRulesAsync_ParsesMinPrice()
    {
        const string json = @"{\"symbols\":[{\"filters\":[
            {\"filterType\":\"PRICE_FILTER\",\"minPrice\":\"0.0001\",\"maxPrice\":\"1000\",\"tickSize\":\"0.0001\"},
            {\"filterType\":\"LOT_SIZE\",\"minQty\":\"1\",\"stepSize\":\"1\"},
            {\"filterType\":\"MIN_NOTIONAL\",\"notional\":\"5\"}
        ]}]}";

        var client = new HttpClient(new StubHttpMessageHandler(json))
        {
            BaseAddress = new Uri("https://example.com")
        };

        var svc = new BinanceApiService(client);

        var method = typeof(BinanceApiService).GetMethod("GetSymbolRulesAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task)method!.Invoke(svc, new object[] { "DOGEUSDT", CancellationToken.None })!;
        await task.ConfigureAwait(false);
        var rules = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var minPriceProp = rules.GetType().GetProperty("MinPrice")!;
        var minPrice = (decimal?)minPriceProp.GetValue(rules);

        Assert.Equal(0.0001m, minPrice);
    }
}

