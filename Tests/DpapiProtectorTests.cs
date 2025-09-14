using System.Text;
using BinanceUsdtTicker.Security;
using Xunit;

namespace BinanceUsdtTicker.Tests
{
    public class DpapiProtectorTests
    {
        [Fact]
        public void ProtectUnprotect_Roundtrip()
        {
            var original = "secret-value";
            var enc = DpapiProtector.Protect(Encoding.UTF8.GetBytes(original));
            var dec = Encoding.UTF8.GetString(DpapiProtector.Unprotect(enc));
            Assert.Equal(original, dec);
        }
    }
}
