using System.Security.Cryptography;
using System.Text;

namespace BinanceUsdtTicker.Security
{
    /// <summary>
    /// Helper for encrypting and decrypting values using Windows DPAPI.
    /// </summary>
    public static class DpapiProtector
    {
        // Application specific additional entropy. Even if leaked, it is
        // useless without the DPAPI keys that are bound to the machine.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("BinanceUsdtTicker.v1:7A4E2E7C-9A7D-4C2A-A4B1-5C3D1E9F");

        public static byte[] Protect(ReadOnlySpan<byte> plain)
            => ProtectedData.Protect(plain.ToArray(), Entropy, DataProtectionScope.LocalMachine);

        public static byte[] Unprotect(ReadOnlySpan<byte> enc)
            => ProtectedData.Unprotect(enc.ToArray(), Entropy, DataProtectionScope.LocalMachine);
    }
}
