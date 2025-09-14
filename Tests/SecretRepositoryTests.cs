using BinanceUsdtTicker.Data;
using Xunit;
using System.Threading.Tasks;

namespace BinanceUsdtTicker.Tests
{
    public class SecretRepositoryTests
    {
        [Fact(Skip="Requires SQL Server instance")] 
        public async Task UpsertAndGetAll_Roundtrip()
        {
            var repo = new SecretRepository("Server=.;Database=Secrets;Integrated Security=True;");
            await repo.UpsertAsync("name", new byte[] { 1 });
            var all = await repo.GetAllAsync();
            Assert.True(all.ContainsKey("name"));
        }
    }
}
