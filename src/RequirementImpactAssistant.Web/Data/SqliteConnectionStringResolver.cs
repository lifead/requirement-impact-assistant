using Microsoft.Data.Sqlite;

namespace RequirementImpactAssistant.Web.Data;

internal static class SqliteConnectionStringResolver
{
    public static string ResolveFileDataSource(string connectionString, string contentRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource;

        if (string.IsNullOrWhiteSpace(dataSource)
            || string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
            || dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var resolvedDataSource = Path.IsPathRooted(dataSource)
            ? Path.GetFullPath(dataSource)
            : Path.GetFullPath(Path.Combine(contentRootPath, dataSource));

        var databaseDirectory = Path.GetDirectoryName(resolvedDataSource);

        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        builder.DataSource = resolvedDataSource;

        return builder.ConnectionString;
    }
}
