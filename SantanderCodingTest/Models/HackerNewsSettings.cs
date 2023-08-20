namespace SantanderCodingTest.Models
{
    public class HackerNewsSettings
    {
        public string BaseUrl { get; set; }
        public string BestStoriesEndpoint { get; set; }
        public string StoryDetailsEndpoint { get; set; }
        public int MaxConcurrentRequests { get; set; }
    }
}
