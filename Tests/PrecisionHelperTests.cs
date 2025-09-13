using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Net.Http;
using BinanceUsdtTicker;
using BinanceUsdtTicker.Helpers;
using Xunit;

public class PrecisionHelperTests
{
    [Fact]
    public async Task QuantizePrice_RoundsDown()
    {
        var api = CreateApi();
        var (_, _, _, price, _) = await api.ApplyOrderPrecisionAsync("BTCUSDT", 57432.1234m, 1m);
        Assert.Equal(57432.1m, price);
    }

    [Fact]
    public async Task QuantityTooSmall_Throws()
    {
        var api = CreateApi();
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await api.ApplyOrderPrecisionAsync("BTCUSDT", null, 0.0009m);
        });
    }

    private static BinanceApiService CreateApi()
    {
        var api = new BinanceApiService(new HttpClient());
        var field = typeof(BinanceApiService).GetField("_symbolFilters", BindingFlags.NonPublic | BindingFlags.Instance);
        var dict = (Dictionary<string, (decimal, decimal, decimal)>)field!.GetValue(api)!;
        dict["BTCUSDT"] = (0.1m, 0.001m, 5m);
        return api;
    }
}
