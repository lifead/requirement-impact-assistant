using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Extensions;

namespace RequirementImpactAssistant.Tests.Configuration;

public sealed class ApplicationConfigurationTests
{
    [Fact]
    public void Configuration_LoadsDevelopmentSqliteConnectionString()
    {
        var configuration = CreateApplicationConfiguration();

        var connectionString = configuration.GetConnectionString("ApplicationDb");

        Assert.Equal(
            "Data Source=App_Data/requirement-impact-assistant.development.db",
            connectionString);
    }

    [Fact]
    public void ApplicationDbConnectionString_DoesNotContainSecrets()
    {
        var configuration = CreateApplicationConfiguration();

        var connectionString = configuration.GetConnectionString("ApplicationDb");

        Assert.NotNull(connectionString);
        Assert.DoesNotContain("Password=", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Pwd=", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User Id=", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Uid=", connectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PersistenceRegistration_RequiresOnlySqliteConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ApplicationDb"] = "Data Source=App_Data/test.db"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddApplicationPersistence(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.NotNull(dbContext);
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", dbContext.Database.ProviderName);
    }

    [Fact]
    public void PersistenceRegistration_FailsWhenConnectionStringIsMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddApplicationPersistence(configuration));

        Assert.Contains("Connection string 'ApplicationDb' is not configured.", exception.Message);
    }

    [Fact]
    public void DesignTimeFactory_CreatesSqliteApplicationDbContext()
    {
        var factory = new ApplicationDbContextFactory(GetWebProjectPath);

        using var dbContext = factory.CreateDbContext([]);

        Assert.NotNull(dbContext);
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", dbContext.Database.ProviderName);
    }

    private static IConfigurationRoot CreateApplicationConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(GetWebProjectPath())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();
    }

    private static string GetWebProjectPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "RequirementImpactAssistant.Web"));
    }
}
