using AspNetExportTemplate.Models;

namespace AspNetExportTemplate.Services
{
    public interface IFrontConnector
    {
        Task<List<T>> MakePaginatedRequestAsync<T>(string url);
        IAsyncEnumerable<T> StreamPaginatedRequestAsync<T>(string url);
        /// <summary>
        /// Attempt to get a total resource count for the given URL (returns null if unavailable).
        /// </summary>
        Task<long?> GetResourceCountAsync(string url);
        Task<byte[]?> GetAttachmentAsync(string url);
    }
}
