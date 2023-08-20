using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SantanderCodingTest.Interfaces;
using SantanderCodingTest.Models;
using HdrHistogram;
using System.Diagnostics;

namespace SantanderCodingTest.Services
{
    public class HackerNewsService : IHackerNewsService
    {
        private readonly string _baseUrl;
        private readonly string _bestStoriesEndpoint;
        private readonly string _storyDetailsEndpoint;
        private readonly HttpClient _client;
        private readonly IMemoryCache _cache;
        private readonly CacheSettings _cacheSettings;
        private readonly LongHistogram _histogramCached;
        private readonly LongHistogram _histogramNonCached;
        private readonly int _maxConcurrentRequests;
        private const string cacheKey = "AllBestStories";

        public HackerNewsService(HttpClient client, IMemoryCache cache, IOptions<CacheSettings> cacheSettings, IOptions<HackerNewsSettings> hackerNewsSettings)
        {
            _client = client;
            _cache = cache;
            _cacheSettings = cacheSettings.Value;
            _baseUrl = hackerNewsSettings.Value.BaseUrl;
            _bestStoriesEndpoint = hackerNewsSettings.Value.BestStoriesEndpoint;
            _storyDetailsEndpoint = hackerNewsSettings.Value.StoryDetailsEndpoint;
            _maxConcurrentRequests = hackerNewsSettings.Value.MaxConcurrentRequests;

            _histogramCached = new LongHistogram(TimeStamp.Seconds(1), 5);
            _histogramNonCached = new LongHistogram(TimeStamp.Seconds(1), 5);
        }

        // Use SemaphoreSlim instead of standard Lock because:
        // allows for cancellation tokens and asynchronous locking through WaitAsync
        // SemaphoreSlim(1, 1) means only one thread can populate the cache.
        private SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        public async Task<IEnumerable<Story>> GetAllBestStoriesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();
            
            if (!_cache.TryGetValue(cacheKey, out IEnumerable<Story> allStories))
            {
                await _cacheLock.WaitAsync(cancellationToken);
                try
                {
                    // Check again inside the lock, as another request might have already populated the cache.
                    if (!_cache.TryGetValue(cacheKey, out allStories))
                    {
                        // The expensive part of the logic - calling the HAcker News Api
                        var storyIds = await _client.GetFromJsonAsync<int[]>($"{_baseUrl}{_bestStoriesEndpoint}");
                        if (storyIds == null || !storyIds.Any())
                        {
                            return Enumerable.Empty<Story>();
                        }

                        var tasks = storyIds.Select(id => FetchStoryDetails(id));
                        var results = await Task.WhenAll(tasks);
                        allStories = results.OrderByDescending(s => s.score);

                        var cacheOptions = new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheSettings.TimeoutInMinutes)
                        };

                        _cache.Set(cacheKey, allStories, cacheOptions);
                        _histogramNonCached.RecordValue(stopwatch.ElapsedTicks);
                    }
                    else
                    {
                        _histogramCached.RecordValue(stopwatch.ElapsedTicks);
                    }
                }
                finally
                {
                    _cacheLock.Release();
                }

            }
            else
            {
                _histogramCached.RecordValue(stopwatch.ElapsedTicks);
            }            
            
            return allStories;
        }

        private async Task<Story> FetchStoryDetails(int id)
        {
            var storyEndpoint = string.Format(_storyDetailsEndpoint, id);
            var url = $"{_baseUrl}{storyEndpoint}";
            var story = await _client.GetFromJsonAsync<Story>(url);

            if (story == null)
            {
                 // Make sure missing stories do not fail whole process. 
                 // Insert error details in title.
                 // May not be correct implementation, but shows I've thought about it.
                 return new Story()
                 {
                     title = $"Failed to fetch story details for ID: {id} with url {url}"
                 };
            }

            return story;
        }

        public void LogPerformanceMetrics()
        {

            var writerCached = new StringWriter();
            var writerNonCached = new StringWriter();
            
            var scalingRatio = OutputScalingFactor.TimeStampToMicroseconds;
            
            _histogramCached.OutputPercentileDistribution(writerCached, outputValueUnitScalingRatio: scalingRatio);
            Debug.WriteLine("Value in microseconds - 1 millionth of a second");
            Debug.WriteLine("Performance when reading values from Cache");
            Debug.WriteLine(writerCached.ToString());
            Debug.WriteLine("-------------------------------------------");
            Debug.WriteLine("Performance when reading values from Hacker News api (not from cache)");

            _histogramNonCached.OutputPercentileDistribution(writerNonCached, outputValueUnitScalingRatio: scalingRatio);
            Debug.WriteLine(writerNonCached.ToString());

            _histogramCached.Reset();
            _histogramNonCached.Reset();
        }
    }
}
