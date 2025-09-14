using BinanceUsdtTicker.Data;
using BinanceUsdtTicker.Runtime;
using BinanceUsdtTicker.Security;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace BinanceUsdtTicker.Tests
{
    public class SecretBootstrapperTests
    {
        private sealed class FakeRepo : SecretRepository
        {
            private readonly Dictionary<string, byte[]> _data;
            public FakeRepo(Dictionary<string, byte[]> data) : base(string.Empty)
            {
                _data = data;
            }
            public override Task<Dictionary<string, byte[]>> GetAllAsync(CancellationToken ct = default)
            {
                return Task.FromResult(new Dictionary<string, byte[]>(_data));
            }
        }

        [Fact]
        public async Task LoadsSecretsIntoCache()
        {
            var encValue = DpapiProtector.Protect(Encoding.UTF8.GetBytes("abc"));
            var repo = new FakeRepo(new Dictionary<string, byte[]> { { "k", encValue } });
            var cache = new SecretCache();
            var bootstrapper = new SecretBootstrapper(NullLogger<SecretBootstrapper>.Instance, repo, cache);
            await bootstrapper.StartAsync(default);
            Assert.Equal("abc", cache.Get("k"));
        }
    }
}
