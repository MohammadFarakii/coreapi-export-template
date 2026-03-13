using AspNetExportTemplate.Models;

namespace AspNetExportTemplate.Services
{
    public interface IFrontConnector
    {
        Task<List<T>> MakePaginatedRequestAsync<T>(string url);
        Task<byte[]?> GetAttachmentAsync(string url);
    }
}
