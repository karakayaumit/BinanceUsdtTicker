using BinanceUsdtTicker.Data;
using BinanceUsdtTicker.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BinanceUsdtTicker.Runtime
{
    /// <summary>
    /// Loads encrypted secrets from the database on startup and stores them in memory cache.
    /// </summary>
    public sealed class SecretBootstrapper : IHostedService
    {
        private readonly ILogger<SecretBootstrapper> _log;
        private readonly SecretRepository _repo;
        private readonly ISecretCache _cache;

        public SecretBootstrapper(ILogger<SecretBootstrapper> log, SecretRepository repo, ISecretCache cache)
        {
            _log = log;
            _repo = repo;
            _cache = cache;
        }

        public async Task StartAsync(CancellationToken ct)
        {
            var enc = await _repo.GetAllAsync(ct);
            int count = 0;
            foreach (var (name, valueEnc) in enc)
            {
                var plainBytes = DpapiProtector.Unprotect(valueEnc);
                var value = System.Text.Encoding.UTF8.GetString(plainBytes);
                _cache.Set(name, value);
                count++;
            }
            _log.LogInformation("Secrets loaded to RAM: {Count}", count);
        }

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
