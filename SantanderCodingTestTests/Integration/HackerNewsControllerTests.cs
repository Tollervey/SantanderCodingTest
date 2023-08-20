using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using SantanderCodingTest.Controllers;
using SantanderCodingTest.Interfaces;
using SantanderCodingTest.Models;
using SantanderCodingTest.Services;
using HdrHistogram;
using System.Diagnostics;

namespace SantanderCodingTestTests.Integration
{
    [TestClass]
    public class HackerNewsControllerIntegrationTests
    {
        private HackerNewsController _hackerNewsController;
        private MemoryCache _cache;
        private IHackerNewsService _hackerNewsService;

        [TestInitialize]
        public void Setup()
        {
            var httpClient = new HttpClient();

            var cacheOptions = new MemoryCacheOptions();
            _cache = new MemoryCache(cacheOptions);

            var cacheSettings = new CacheSettings { TimeoutInMinutes = 2 };
            var cacheSettingsOptions = Options.Create(cacheSettings);

            // Create an instance of HackerNewsSettings
            var hackerNewsSettings = new HackerNewsSettings
            {
                BaseUrl = "https://hacker-news.firebaseio.com/v0",
                BestStoriesEndpoint = "/beststories.json",
                StoryDetailsEndpoint = "/item/{0}.json"
            };
            var hackerNewsSettingsOptions = Options.Create(hackerNewsSettings);

            _hackerNewsService = new HackerNewsService(httpClient, _cache, cacheSettingsOptions, hackerNewsSettingsOptions);

            _hackerNewsController = new HackerNewsController(_hackerNewsService);
        }

        [TestMethod]
        public async Task GetBestStories_NEquals5_ResultCountIs5()
        {
            var actionResult = await _hackerNewsController.GetBestStories(5);
            var okResult = actionResult as OkObjectResult;
            Assert.IsNotNull(okResult);

            var stories = okResult.Value as IEnumerable<Story>;
            Assert.IsNotNull(stories);
            Assert.AreEqual(5, stories.ToList().Count);
        }

        [TestMethod]
        public async Task GetBestStories_ScoresAreOrderedByHighest()
        {
            var actionResult = await _hackerNewsController.GetBestStories(5);
            var okResult = actionResult as OkObjectResult;
            Assert.IsNotNull(okResult);

            var stories = okResult.Value as IEnumerable<Story>;
            Assert.IsNotNull(stories);

            var scores = stories.Select(s => s.score).ToList();
            Assert.IsTrue(scores.SequenceEqual(scores.OrderByDescending(s => s)), "Scores are not ordered by highest first.");
        }

        [TestMethod]
        public async Task GetBestStories_JsonFormatIsValid()
        {
            var actionResult = await _hackerNewsController.GetBestStories(5);
            var okResult = actionResult as OkObjectResult;
            Assert.IsNotNull(okResult);

            // Serialize the object to JSON
            var json = JsonConvert.SerializeObject(okResult.Value);
            var jsonArray = JArray.Parse(json);

            foreach (var item in jsonArray)
            {
                // Check required properties exist and are of the correct type
                Assert.IsTrue(item["title"] != null && item["title"].Type == JTokenType.String);
                Assert.IsTrue(item["uri"] != null && item["uri"].Type == JTokenType.String);
                Assert.IsTrue(item["postedBy"] != null && item["postedBy"].Type == JTokenType.String);
                Assert.IsTrue(item["time"] != null && item["time"].Type == JTokenType.Date);
                Assert.IsTrue(item["score"] != null && item["score"].Type == JTokenType.Integer);
                Assert.IsTrue(item["commentCount"] != null && item["commentCount"].Type == JTokenType.Integer);

                // Ensure no additional properties are present
                var allowedProperties = new[] { "title", "uri", "postedBy", "time", "score", "commentCount" };
                foreach (var property in item.Children<JProperty>())
                {
                    Assert.IsTrue(allowedProperties.Contains(property.Name), $"Unexpected property {property.Name} found.");
                }
            }
        }

        [TestMethod]
        public async Task TestGetBestStoriesApiMultipleCalls_OutputHistogramToDebug()
        {
            var callCount = 1000; // number of times to call the API
            var histogram = new LongHistogram(TimeStamp.Seconds(1), 5);
            for (int i = 0; i < callCount; i++)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var response = await _hackerNewsController.GetBestStories(10);
                               
                histogram.RecordValue(stopwatch.ElapsedTicks);

            }
            var writer = new StringWriter();
            var scalingRatio = OutputScalingFactor.TimeStampToMicroseconds;
            histogram.OutputPercentileDistribution(writer, outputValueUnitScalingRatio: scalingRatio);
            Debug.WriteLine(writer.ToString());
            histogram.Reset();
        }

        [TestCleanup]
        public void TearDown()
        {
            _cache.Dispose();
        }
    }
}
