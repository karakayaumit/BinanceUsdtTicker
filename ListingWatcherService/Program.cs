using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ListingWatcher;
using System;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();

if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(o => o.ServiceName = "Binance Watcher");
    builder.Logging.AddEventLog(o => o.SourceName = "ListingWatcherService");
}

builder.Services.AddHostedService<ListingWatcherService>();

await builder.Build().RunAsync();
