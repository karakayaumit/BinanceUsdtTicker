using System.Collections.Concurrent;

namespace BinanceUsdtTicker.Runtime
{
    public interface ISecretCache
    {
        string Get(string name);
        void Set(string name, string value);
    }

    /// <summary>
    /// In-memory thread safe cache for secrets.
    /// </summary>
    public sealed class SecretCache : ISecretCache
    {
        private readonly ConcurrentDictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);

        public string Get(string name) => _secrets[name];

        public void Set(string name, string value)
        {
            _secrets[name] = value;
        }
    }
}
