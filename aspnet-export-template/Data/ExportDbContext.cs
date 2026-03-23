using AspNetExportTemplate.Models;
using Microsoft.EntityFrameworkCore;

namespace AspNetExportTemplate.Data
{
    public class ExportDbContext : DbContext
    {
        public ExportDbContext(DbContextOptions<ExportDbContext> options) : base(options) { }

        public DbSet<FailedExport> FailedExports { get; set; }
    }
}
