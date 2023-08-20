using Microsoft.AspNetCore.Mvc;
using SantanderCodingTest.Interfaces;

namespace SantanderCodingTest.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HackerNewsController : ControllerBase
    {
        private readonly IHackerNewsService _hackerNewsService;

        public HackerNewsController(IHackerNewsService hackerNewsService)
        {
            _hackerNewsService = hackerNewsService;
        }

        [HttpGet("beststories")]
        //[OutputCache(Duration = 120, VaryByQueryKeys = new[] { "n" })] Might be useful depending upon typical usage and traffic to cache at the API output level also
        public async Task<IActionResult> GetBestStories(int n = 10)
        {
            if (n <= 0)
            {
                return BadRequest("The value of 'n' should be greater than 0.");
            }

            var allStories = await _hackerNewsService.GetAllBestStoriesAsync();
            // already ordered and cached in the HackerNewsService class
            var topStories = allStories.Take(n);
            return Ok(topStories);
        }
    }
}