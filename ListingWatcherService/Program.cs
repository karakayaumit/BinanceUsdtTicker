using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BinanceUsdtTicker;

Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .ConfigureServices(services =>
    {
        services.AddHostedService<ListingWatcherService>();
    })
    .Build()
    .Run();
