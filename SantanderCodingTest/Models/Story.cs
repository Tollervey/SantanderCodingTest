using Newtonsoft.Json;

namespace SantanderCodingTest.Models
{
    public class Story
    {
        [JsonProperty("title")]
        public string title { get; set; }

        [JsonProperty("uri")]
        public string url { get; set; }

        [JsonProperty("postedBy")]
        public string by { get; set; }

        [JsonProperty("score")]
        public int score { get; set; }

        [JsonProperty("commentCount")]
        public int descendants { get; set; }

        [JsonIgnore]
        public long time { get; set; }

        // https://github.com/HackerNews/API - Creation date of the item, in Unix Time.
        [JsonProperty("time")]
        public DateTime creationDate => DateTimeOffset.FromUnixTimeSeconds(time).DateTime;
        
    }
}
