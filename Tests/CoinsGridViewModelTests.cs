using System.Linq;
using BinanceUsdtTicker.ViewModels.Coins;
using Xunit;

namespace BinanceUsdtTicker.Tests;

public class CoinsGridViewModelTests
{
    [Fact]
    public void ApplyTick_AssignsBaselinePriceWhenMissing()
    {
        var vm = new CoinsGridViewModel();
        var update = new TickerUpdate("BTCUSDT", 100m, 0.0, 10m);

        vm.ApplyTick(update);

        var row = Assert.Single(vm.Items);
        Assert.Equal(100m, row.Price);
        Assert.Equal(100m, row.BaselinePrice);
    }
}
