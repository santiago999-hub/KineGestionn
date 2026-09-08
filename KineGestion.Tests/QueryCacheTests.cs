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
            var callCount = 0;

            async Task<int> Factory()
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(75);
                return 42;
            }

            var tasks = Enumerable.Range(0, 8)
                .Select(_ => QueryCache.GetOrCreateAsync("querycache-test:concurrency", Factory, TimeSpan.FromSeconds(2)));

            var results = await Task.WhenAll(tasks);

            Assert.All(results, value => Assert.Equal(42, value));
            Assert.Equal(1, callCount);
        }

        [Fact]
        public async Task GetOrCreateAsync_ShouldNotCache_WhenTtlIsZeroOrNegative()
        {
            var callCount = 0;

            async Task<int> Factory()
            {
                Interlocked.Increment(ref callCount);
                await Task.Yield();
                return 7;
            }

            var first = await QueryCache.GetOrCreateAsync("querycache-test:no-cache", Factory, TimeSpan.Zero);
            var second = await QueryCache.GetOrCreateAsync("querycache-test:no-cache", Factory, TimeSpan.FromMilliseconds(-1));

            Assert.Equal(7, first);
            Assert.Equal(7, second);
            Assert.Equal(2, callCount);
        }

        [Fact]
        public async Task GetOrCreateAsync_ShouldRecompute_WhenEntryExpired()
        {
            var callCount = 0;

            async Task<int> Factory()
            {
                Interlocked.Increment(ref callCount);
                await Task.Yield();
                return callCount;
            }

            var first = await QueryCache.GetOrCreateAsync("querycache-test:expiring", Factory, TimeSpan.FromMilliseconds(20));
            await Task.Delay(50);
            var second = await QueryCache.GetOrCreateAsync("querycache-test:expiring", Factory, TimeSpan.FromMilliseconds(20));

            Assert.Equal(1, first);
            Assert.Equal(2, second);
            Assert.Equal(2, callCount);
        }

        [Fact]
        public async Task GetOrCreateAsync_ShouldKeepSingleFlight_AcrossConcurrentRounds()
        {
            var callCount = 0;

            async Task<int> Factory()
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(30);
                return 99;
            }

            for (var round = 0; round < 5; round++)
            {
                var key = $"querycache-test:round:{round}";
                var tasks = Enumerable.Range(0, 10)
                    .Select(_ => QueryCache.GetOrCreateAsync(key, Factory, TimeSpan.FromSeconds(1)));

                var results = await Task.WhenAll(tasks);
                Assert.All(results, value => Assert.Equal(99, value));
            }

            Assert.Equal(5, callCount);
        }
    }
}