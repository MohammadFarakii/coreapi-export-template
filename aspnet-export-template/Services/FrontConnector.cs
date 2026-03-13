using AspNetExportTemplate.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AspNetExportTemplate.Services
{
    public class FrontConnector : IFrontConnector
    {
        private readonly HttpClient _client;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
        private readonly string _apiKey;

        public FrontConnector(HttpClient client)
        {
            _client = client;
            _apiKey = Environment.GetEnvironmentVariable("API_KEY") ?? string.Empty;
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }
        }

        public async Task<List<T>> MakePaginatedRequestAsync<T>(string url)
        {
            var results = new List<T>();
            string? next = url;
            while (!string.IsNullOrEmpty(next))
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, next);
                using var res = await _client.SendAsync(req);

                if (res.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    await HandleRateLimitAsync(res);
                    continue;
                }

                res.EnsureSuccessStatusCode();
                var stream = await res.Content.ReadAsStreamAsync();
                var apiResponse = await JsonSerializer.DeserializeAsync<ApiResponse<T>>(stream, _jsonOptions);
                if (apiResponse?.Results != null)
                {
                    results.AddRange(apiResponse.Results);
                }
                next = apiResponse?.Pagination?.Next;
            }
            return results;
        }

        public async Task<byte[]?> GetAttachmentAsync(string url)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var res = await _client.SendAsync(req);
            if (res.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                await HandleRateLimitAsync(res);
                return await GetAttachmentAsync(url);
            }
            res.EnsureSuccessStatusCode();
            return await res.Content.ReadAsByteArrayAsync();
        }

        private async Task HandleRateLimitAsync(HttpResponseMessage res)
        {
            if (res.Headers.TryGetValues("retry-after", out var values))
            {
                var v = values.FirstOrDefault();
                if (int.TryParse(v, out var seconds))
                {
                    await Task.Delay(seconds * 1000);
                    return;
                }
            }
            // Fallback delay
            await Task.Delay(1000);
        }
    }
}
