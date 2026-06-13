using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RequirementImpactAssistant.Web.Data;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private readonly Func<string> projectPathResolver;

    public ApplicationDbContextFactory()
        : this(ResolveProjectPath)
    {
    }

    internal ApplicationDbContextFactory(Func<string> projectPathResolver)
    {
        this.projectPathResolver = projectPathResolver ?? throw new ArgumentNullException(nameof(projectPathResolver));
    }

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var environmentName =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var projectPath = projectPathResolver();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(projectPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("ApplicationDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ApplicationDb' is not configured.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static string ResolveProjectPath()
    {
        return FindProjectPath(Directory.GetCurrentDirectory())
            ?? FindProjectPath(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                "Could not locate the RequirementImpactAssistant.Web project directory for design-time DbContext creation.");
    }

    private static string? FindProjectPath(string startPath)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startPath));

        while (directory is not null)
        {
            var appSettingsPath = Path.Combine(directory.FullName, "appsettings.json");
            var projectFilePath = Path.Combine(
                directory.FullName,
                "RequirementImpactAssistant.Web.csproj");

            if (File.Exists(appSettingsPath) && File.Exists(projectFilePath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
