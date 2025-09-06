using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ListingWatcher;
using System;

var builder = Host.CreateDefaultBuilder(args);

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
