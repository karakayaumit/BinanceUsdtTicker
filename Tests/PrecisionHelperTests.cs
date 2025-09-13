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
        var (_, price, _) = await api.ApplyOrderPrecisionAsync("BTCUSDT", 57432.1234m, 1m);
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

    [Fact]
    public void ToInvariantString_LeavesIntegerUnchanged()
    {
        var method = typeof(BinanceApiService).GetMethod("ToInvariantString", BindingFlags.NonPublic | BindingFlags.Static);
        var result = (string)method!.Invoke(null, new object[] { 116500m })!;
        Assert.Equal("116500", result);
    }

    [Fact]
    public void BuildQuery_LeavesIntegerUnchanged()
    {
        var method = typeof(BinanceRestClientBase).GetMethod("BuildQuery", BindingFlags.NonPublic | BindingFlags.Static);
        var dict = new Dictionary<string, string> { ["price"] = "116500" };
        var result = (string)method!.Invoke(null, new object[] { dict })!;
        Assert.Equal("price=116500", result);
    }

    private static BinanceApiService CreateApi()
    {
        var api = new BinanceApiService(new HttpClient());
        var field = typeof(BinanceApiService).GetField("_symbolFilters", BindingFlags.NonPublic | BindingFlags.Instance);
        var dict = (Dictionary<string, SymbolPrecision>)field!.GetValue(api)!;
        dict["BTCUSDT"] = new SymbolPrecision(0.1m, 0.001m, 5m);
        return api;
    }
}
