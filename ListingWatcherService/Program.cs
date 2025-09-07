using BinanceUsdtTicker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<ISymbolExtractor, RegexSymbolExtractor>();
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
