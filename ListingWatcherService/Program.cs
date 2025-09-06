using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ListingWatcher;
using System;

var builder = Host.CreateDefaultBuilder(args)
.UseWindowsService()
.ConfigureServices(services =>
{
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

builder.ConfigureServices(services =>
    {
        services.AddHostedService<ListingWatcherService>();
    })
    .Build()
    .Run();

await builder.Build().RunAsync();