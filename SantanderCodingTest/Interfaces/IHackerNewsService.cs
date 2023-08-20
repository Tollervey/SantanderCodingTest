using SantanderCodingTest.Models;

namespace SantanderCodingTest.Interfaces
{
    public interface IHackerNewsService
    {
        Task<IEnumerable<Story>> GetAllBestStoriesAsync(CancellationToken cancellationToken = default);
    }
}
