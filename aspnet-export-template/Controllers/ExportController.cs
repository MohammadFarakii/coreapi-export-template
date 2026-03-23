using AspNetExportTemplate.Models;
using AspNetExportTemplate.Services;
using Microsoft.AspNetCore.Mvc;

namespace AspNetExportTemplate.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExportController : ControllerBase
    {
        private readonly IFrontConnector _connector;
        private readonly ExportService _exportService;

        public ExportController(IFrontConnector connector, ExportService exportService)
        {
            _connector = connector;
            _exportService = exportService;
        }

        [HttpGet("inboxes")]
        public async Task<IActionResult> ListInboxes()
        {
            var inboxes = await _connector.MakePaginatedRequestAsync<Inbox>("https://api2.frontapp.com/inboxes");
            return Ok(inboxes);
        }

        [HttpPost("export/inbox/{id}")]
        public async Task<IActionResult> ExportInbox(string id, [FromBody] ExportOptions? options)
        {
            var inboxes = await _connector.MakePaginatedRequestAsync<Inbox>("https://api2.frontapp.com/inboxes");
            var inbox = inboxes.FirstOrDefault(i => i.Id == id);
            if (inbox == null) return NotFound();
            var exported = await _exportService.ExportInboxAsync(inbox, options);
            return Ok(new { exported });
        }

        public class SearchRequest
        {
            public string AzureFolderName { get; set; } = string.Empty;
            public string Query { get; set; } = string.Empty;
            public long? After { get; set; }
            public List<string>? Statuses { get; set; }
            public ExportOptions? Options { get; set; }
        }

        [HttpPost("export/search")]
        public async Task<IActionResult> ExportSearch([FromBody] SearchRequest req)
        {
            var exported = await _exportService.ExportSearchAsync(req.AzureFolderName, req.Query, req.After, req.Statuses, req.Options);
            return Ok(new { exported });
        }

        [HttpPost("export/inboxes")]
        public async Task<IActionResult> ExportAllInboxes([FromBody] ExportOptions? options)
        {
            var inboxes = await _connector.MakePaginatedRequestAsync<Inbox>("https://api2.frontapp.com/inboxes");
            int totalExported = 0;
            foreach (var inbox in inboxes)
            {
                var exported = await _exportService.ExportInboxAsync(inbox, options);
                totalExported += exported;
            }
            return Ok(new { inboxCount = inboxes.Count, exported = totalExported });
        }

        [HttpGet("export/failed")]
        public async Task<IActionResult> GetFailedExports()
        {
            var failed = await _exportService.GetFailedExportsAsync();
            return Ok(failed);
        }

        [HttpPost("export/retry")]
        public async Task<IActionResult> RetryFailedExports()
        {
            var (succeeded, failed) = await _exportService.RetryFailedExportsAsync();
            return Ok(new { succeeded, failed });
        }
    }
}