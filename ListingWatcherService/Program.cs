using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ListingWatcher;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(o => o.ServiceName = "Binance Watcher");

builder.Logging.ClearProviders();
builder.Logging.AddEventLog(o => o.SourceName = "ListingWatcherService");

builder.Services.AddHostedService<ListingWatcherService>();

await builder.Build().RunAsync();
