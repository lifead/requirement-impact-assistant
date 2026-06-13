using Microsoft.EntityFrameworkCore;

namespace RequirementImpactAssistant.Web.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
}
