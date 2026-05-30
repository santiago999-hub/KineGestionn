using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KineGestion.Core.Services;
using Xunit;

namespace KineGestion.Tests
{
    public class QueryCacheTests
    {
        [Fact]
        public async Task GetOrCreateAsync_ShouldExecuteFactoryOnce_WhenConcurrentMisses()
        {
            QueryCache.ClearAll();
            var callCount = 0;

            async Task<int> Factory()
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(75);
                return 42;
            }

            var tasks = Enumerable.Range(0, 8)
                .Select(_ => QueryCache.GetOrCreateAsync("sessions:admin:paged:concurrency-test", Factory, TimeSpan.FromSeconds(2)));

            var results = await Task.WhenAll(tasks);

            Assert.All(results, value => Assert.Equal(42, value));
            Assert.Equal(1, callCount);
        }
    }
}