using BinanceUsdtTicker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ListingWatcher;
using BinanceUsdtTicker.Data;
using BinanceUsdtTicker.Runtime;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.AddSingleton<ISymbolExtractor, RegexSymbolExtractor>();
        services.AddSingleton<ISecretCache, SecretCache>();
        services.AddSingleton(sp => new SecretRepository(ctx.Configuration.GetConnectionString("Listings")!));
        services.AddHostedService<SecretBootstrapper>();
        services.AddHostedService<ListingWatcherService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddEventLog(o => o.SourceName = "ListingWatcherService");
    });

if (OperatingSystem.IsWindows())
{
    builder.UseWindowsService();
}

await builder.Build().RunAsync();
