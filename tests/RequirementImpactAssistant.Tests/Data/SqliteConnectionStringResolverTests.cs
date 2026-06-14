using Microsoft.Data.Sqlite;
using RequirementImpactAssistant.Web.Data;

namespace RequirementImpactAssistant.Tests.Data;

public sealed class SqliteConnectionStringResolverTests
{
    [Fact]
    public void ResolveFileDataSource_MemoryDataSource_ReturnsConnectionStringWithoutCreatingDirectory()
    {
        var contentRootPath = CreateTempDirectory();
        var connectionString = "Data Source=:memory:";

        var resolvedConnectionString = SqliteConnectionStringResolver.ResolveFileDataSource(
            connectionString,
            contentRootPath);

        Assert.Equal(connectionString, resolvedConnectionString);
        Assert.Empty(Directory.EnumerateFileSystemEntries(contentRootPath));
    }

    [Fact]
    public void ResolveFileDataSource_AbsoluteDataSource_KeepsAbsolutePathAndCreatesDirectory()
    {
        var contentRootPath = CreateTempDirectory();
        var databaseDirectory = Path.Combine(CreateTempDirectory(), "App_Data", "nested");
        var databasePath = Path.Combine(databaseDirectory, "application.db");
        var connectionString = $"Data Source={databasePath}";

        var resolvedConnectionString = SqliteConnectionStringResolver.ResolveFileDataSource(
            connectionString,
            contentRootPath);

        var builder = new SqliteConnectionStringBuilder(resolvedConnectionString);

        Assert.Equal(Path.GetFullPath(databasePath), builder.DataSource);
        Assert.True(Directory.Exists(databaseDirectory));
        Assert.Empty(Directory.EnumerateFileSystemEntries(contentRootPath));
    }

    [Fact]
    public void ResolveFileDataSource_FileUriDataSource_ReturnsConnectionStringWithoutCreatingDirectory()
    {
        var contentRootPath = CreateTempDirectory();
        var connectionString = "Data Source=file:application.db?mode=memory&cache=shared";

        var resolvedConnectionString = SqliteConnectionStringResolver.ResolveFileDataSource(
            connectionString,
            contentRootPath);

        Assert.Equal(connectionString, resolvedConnectionString);
        Assert.Empty(Directory.EnumerateFileSystemEntries(contentRootPath));
    }

    [Fact]
    public void ResolveFileDataSource_RelativeDataSource_CreatesDirectoryUnderContentRoot()
    {
        var contentRootPath = CreateTempDirectory();
        var relativeDatabasePath = Path.Combine("App_Data", "nested", "application.db");
        var expectedDatabasePath = Path.GetFullPath(Path.Combine(contentRootPath, relativeDatabasePath));
        var expectedDatabaseDirectory = Path.GetDirectoryName(expectedDatabasePath);
        var connectionString = $"Data Source={relativeDatabasePath}";

        var resolvedConnectionString = SqliteConnectionStringResolver.ResolveFileDataSource(
            connectionString,
            contentRootPath);

        var builder = new SqliteConnectionStringBuilder(resolvedConnectionString);

        Assert.Equal(expectedDatabasePath, builder.DataSource);
        Assert.NotNull(expectedDatabaseDirectory);
        Assert.True(Directory.Exists(expectedDatabaseDirectory));
    }

    private static string CreateTempDirectory()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        Directory.CreateDirectory(directoryPath);

        return directoryPath;
    }
}
