using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SantanderCodingTest.Models;
using SantanderCodingTest.Services;

namespace SantanderCodingTestTests.Integration
{

    [TestClass]
    public class HackerNewsServiceTests
    {
        private readonly HttpClient _httpClient;
        private readonly MemoryCache _cache;
        private readonly HackerNewsService _service;

        public TestContext TestContext { get; set; }

        public HackerNewsServiceTests()
        {
            _httpClient = new HttpClient();
            _cache = new MemoryCache(new MemoryCacheOptions());
            var loggerFactory = new LoggerFactory();

            var cacheSettings = new CacheSettings { TimeoutInMinutes = 2 };
            var cacheSettingsOptions = Options.Create(cacheSettings);

            var hackerNewsSettings = new HackerNewsSettings
            {
                BaseUrl = "https://hacker-news.firebaseio.com/v0",
                BestStoriesEndpoint = "/beststories.json",
                StoryDetailsEndpoint = "/item/{0}.json"
            };
            var hackerNewsSettingsOptions = Options.Create(hackerNewsSettings);

            _service = new HackerNewsService(_httpClient, _cache, cacheSettingsOptions, hackerNewsSettingsOptions);
        }

        [TestMethod]
        public async Task GetAllBestStoriesAsync_UsesCache()
        {
            Assert.IsTrue(_cache.Count == 0);

             var firstCallStories = await _service.GetAllBestStoriesAsync();

            Assert.IsTrue(_cache.Count == 1);

            _httpClient.DefaultRequestHeaders.Clear();

            var secondCallStories = await _service.GetAllBestStoriesAsync();

            Assert.IsTrue(_cache.Count == 1);

            CollectionAssert.AreEqual(firstCallStories.ToList(), secondCallStories.ToList()); // Ensure both calls return the same data
        }

        [TestMethod]
        public async Task TestPerformanceMetricsAndClearCacheHalfWayThough()
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(10)); // make sure test doesn't take longer than 10 seconds otherwise throw

            int callCount = 1000;  // equivalent to calling api 1000 times
            for (int i = 0; i < callCount; i++)
            {
                // This ensures we cancel the call if it takes too long or the entire test duration is surpassed
                cts.Token.ThrowIfCancellationRequested();

                await _service.GetAllBestStoriesAsync(cts.Token);

                if (i == callCount / 2) // halfway through the calls
                {
                    _cache.Remove("AllBestStories");
                }

                //await Task.Delay(100, cts.Token); // Delay between calls, can also be canceled now
            }

            // Because _cache.Remove("AllBestStories") only called once in test,
            // Cache should only be populated twice, once for first call, and
            // Once half way through. Therefore output should only show two log counts
            // when calling Hacker News api. See Debug Trace in Test Explorer
            _service.LogPerformanceMetrics();
        }

        [TestMethod]
        public async Task TestPerformanceMetrics()
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(10)); // make sure test doesn't take longer than 10 seconds otherwise throw

            int callCount = 1000;  // equivalent to calling api 1000 times
            for (int i = 0; i < callCount; i++)
            {
                // This ensures we cancel the call if it takes too
                cts.Token.ThrowIfCancellationRequested();

                await _service.GetAllBestStoriesAsync(cts.Token);
            }

            _service.LogPerformanceMetrics();
        }
    }
}
