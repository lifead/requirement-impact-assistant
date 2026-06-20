using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Tests.Data;

public sealed class ApplicationDbContextModelTests
{
    [Fact]
    public void Model_MapsMvpDomainEntitiesToExpectedTables()
    {
        using var dbContext = CreateDbContext();
        var model = dbContext.Model;

        Assert.Equal("Analyses", model.FindEntityType(typeof(Analysis))?.GetTableName());
        Assert.Equal("ContextFragments", model.FindEntityType(typeof(ContextFragment))?.GetTableName());
        Assert.Equal("AiAnalysisResults", model.FindEntityType(typeof(AiAnalysisResult))?.GetTableName());
        Assert.Equal("ExpertEvaluations", model.FindEntityType(typeof(ExpertEvaluation))?.GetTableName());
        Assert.Equal("ExpertEvaluatedItems", model.FindEntityType(typeof(ExpertEvaluatedItem))?.GetTableName());
        Assert.Equal("ExpertMissedItems", model.FindEntityType(typeof(ExpertMissedItem))?.GetTableName());
        Assert.Equal("ExpertCorrections", model.FindEntityType(typeof(ExpertCorrection))?.GetTableName());
        Assert.Equal("ExpertConclusions", model.FindEntityType(typeof(ExpertConclusion))?.GetTableName());
    }

    [Theory]
    [InlineData("ImpactMaps")]
    [InlineData("ImpactMapAffectedRequirements")]
    [InlineData("ImpactMapAffectedTasks")]
    [InlineData("ImpactMapAffectedProjectDecisions")]
    [InlineData("ImpactMapAffectedApiInterfacesDocumentsTests")]
    [InlineData("ImpactMapAffectedArchitecturalConstraints")]
    [InlineData("ImpactMapAffectedOrganizationalContextItems")]
    [InlineData("ImpactMapContradictions")]
    [InlineData("ImpactMapMissingInformation")]
    [InlineData("ImpactMapClarificationQuestions")]
    [InlineData("ImpactMapRisks")]
    [InlineData("ImpactMapOptionsForExpertReview")]
    public void Model_MapsImpactMapOwnedTypesToExpectedTables(string tableName)
    {
        using var dbContext = CreateDbContext();
        var mappedTables = dbContext.Model
            .GetEntityTypes()
            .Select(entityType => entityType.GetTableName())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(tableName, mappedTables);
    }

    [Fact]
    public void Model_MapsExpertEvaluationChildItemsWithShadowForeignKeys()
    {
        using var dbContext = CreateDbContext();
        var model = dbContext.Model;

        AssertExpertEvaluationForeignKey<ExpertEvaluatedItem>(model);
        AssertExpertEvaluationForeignKey<ExpertMissedItem>(model);
        AssertExpertEvaluationForeignKey<ExpertCorrection>(model);
    }

    [Fact]
    public void Model_MapsProjectRequestTypeAsRequiredStringColumn()
    {
        using var dbContext = CreateDbContext();
        var entityType = dbContext.Model.FindEntityType(typeof(Analysis));
        var property = entityType?.FindProperty(nameof(Analysis.ProjectRequestType));

        Assert.NotNull(property);
        Assert.Equal(typeof(ProjectRequestType), property.ClrType);
        Assert.Equal("TEXT", property.GetColumnType());
        Assert.Equal(80, property.GetMaxLength());
        Assert.False(property.IsNullable);
        Assert.Equal(typeof(string), property.GetTypeMapping().Converter?.ProviderClrType);
    }

    [Fact]
    public void Model_MapsAiAnalysisResultMetadataWithRetrievedContextItemStorage()
    {
        using var dbContext = CreateDbContext();
        var model = dbContext.Model;
        var resultEntity = model.FindEntityType(typeof(AiAnalysisResult));
        var retrievedContextItemEntity = model.GetEntityTypes()
            .SingleOrDefault(entityType => entityType.ClrType == typeof(RetrievedContextItem));

        Assert.NotNull(resultEntity);
        Assert.Contains(
            resultEntity.GetNavigations(),
            navigation => navigation.Name == nameof(AiAnalysisResult.Metadata)
                && navigation.TargetEntityType.GetTableName() == "AiAnalysisResults");

        Assert.NotNull(retrievedContextItemEntity);
        Assert.Equal("RetrievedContextItems", retrievedContextItemEntity.GetTableName());
        Assert.True(retrievedContextItemEntity.IsOwned());
        Assert.Contains(
            retrievedContextItemEntity.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Any(property => property.Name == "AiAnalysisResultId")
                && foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
        Assert.Contains(
            retrievedContextItemEntity.GetProperties(),
            property => property.Name == "Ordinal"
                && retrievedContextItemEntity.FindPrimaryKey()!.Properties.Contains(property));
    }

    [Fact]
    public async Task Migration_AddsAiAnalysisResultMetadataColumnsAndRetrievedContextItemStorage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var dbContext = new ApplicationDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
        }

        var tableNames = await ReadTableNamesAsync(connection);
        Assert.Contains("RetrievedContextItems", tableNames);
        Assert.DoesNotContain("RetrievalTraces", tableNames);
        Assert.DoesNotContain("RagTraces", tableNames);
        Assert.DoesNotContain("ExternalProviderResponses", tableNames);

        var analysisColumns = await ReadTableColumnsAsync(connection, "Analyses");
        Assert.True(analysisColumns.TryGetValue("ProjectRequestType", out var projectRequestType));
        Assert.False(projectRequestType.IsNullable);
        Assert.Equal("'Other'", projectRequestType.DefaultValue);

        var columns = await ReadTableColumnsAsync(connection, "AiAnalysisResults");
        Assert.True(columns.TryGetValue("AnalysisMode", out var analysisMode));
        Assert.False(analysisMode.IsNullable);
        Assert.Equal("'DirectLlm'", analysisMode.DefaultValue);

        Assert.True(columns.TryGetValue("MetadataEngineName", out var metadataEngineName));
        Assert.True(metadataEngineName.IsNullable);

        Assert.True(columns.TryGetValue("MetadataProviderName", out var metadataProviderName));
        Assert.True(metadataProviderName.IsNullable);

        Assert.True(columns.TryGetValue("MetadataAdapterName", out var metadataAdapterName));
        Assert.True(metadataAdapterName.IsNullable);

        Assert.True(columns.TryGetValue("MetadataModelWorkflowProfileName", out var metadataModelWorkflowProfileName));
        Assert.True(metadataModelWorkflowProfileName.IsNullable);

        Assert.True(columns.TryGetValue("RetrievedContextState", out var retrievedContextState));
        Assert.False(retrievedContextState.IsNullable);
        Assert.Equal("'Unavailable'", retrievedContextState.DefaultValue);

        Assert.True(columns.TryGetValue("Warnings", out var warnings));
        Assert.True(warnings.IsNullable);

        Assert.True(columns.TryGetValue("ManualContextForwardedToExternalAiOrRag", out var manualContextFlag));
        Assert.False(manualContextFlag.IsNullable);
        Assert.Equal("0", manualContextFlag.DefaultValue);

        var retrievedContextColumns = await ReadTableColumnsAsync(connection, "RetrievedContextItems");
        AssertRequiredColumn(retrievedContextColumns, "AiAnalysisResultId");
        AssertRequiredColumn(retrievedContextColumns, "Ordinal");
        AssertRequiredColumn(retrievedContextColumns, "SourceTitle");
        AssertOptionalColumn(retrievedContextColumns, "SourceId");
        AssertOptionalColumn(retrievedContextColumns, "ExternalReference");
        AssertOptionalColumn(retrievedContextColumns, "FragmentId");
        AssertOptionalColumn(retrievedContextColumns, "Text");
        AssertOptionalColumn(retrievedContextColumns, "Excerpt");
        AssertOptionalColumn(retrievedContextColumns, "UrlOrReference");
        AssertOptionalColumn(retrievedContextColumns, "Rank");
        AssertOptionalColumn(retrievedContextColumns, "Score");
        AssertOptionalColumn(retrievedContextColumns, "ProviderName");
        AssertOptionalColumn(retrievedContextColumns, "AdapterName");
        AssertRequiredColumn(retrievedContextColumns, "Completeness");
        AssertOptionalColumn(retrievedContextColumns, "WarningOrLimitationNote");
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<HashSet<string>> ReadTableNamesAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

        await using var reader = await command.ExecuteReaderAsync();

        var tableNames = new HashSet<string>(StringComparer.Ordinal);

        while (await reader.ReadAsync())
        {
            tableNames.Add(reader.GetString(0));
        }

        return tableNames;
    }

    private static async Task<Dictionary<string, SqliteColumnInfo>> ReadTableColumnsAsync(
        SqliteConnection connection,
        string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        await using var reader = await command.ExecuteReaderAsync();

        var columns = new Dictionary<string, SqliteColumnInfo>(StringComparer.Ordinal);

        while (await reader.ReadAsync())
        {
            var name = reader.GetString(1);
            var isNullable = reader.GetInt32(3) == 0;
            var defaultValue = reader.IsDBNull(4) ? null : reader.GetString(4);

            columns.Add(name, new SqliteColumnInfo(isNullable, defaultValue));
        }

        return columns;
    }

    private static void AssertExpertEvaluationForeignKey<TEntity>(IModel model)
    {
        var entityType = model.FindEntityType(typeof(TEntity));

        Assert.NotNull(entityType);
        Assert.Contains(
            entityType.GetForeignKeys(),
            foreignKey =>
                foreignKey.PrincipalEntityType.ClrType == typeof(ExpertEvaluation)
                && foreignKey.Properties.Any(property => property.Name == "ExpertEvaluationId")
                && foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
    }

    private static void AssertRequiredColumn(
        IReadOnlyDictionary<string, SqliteColumnInfo> columns,
        string columnName)
    {
        Assert.True(columns.TryGetValue(columnName, out var column));
        Assert.False(column.IsNullable);
    }

    private static void AssertOptionalColumn(
        IReadOnlyDictionary<string, SqliteColumnInfo> columns,
        string columnName)
    {
        Assert.True(columns.TryGetValue(columnName, out var column));
        Assert.True(column.IsNullable);
    }

    private sealed record SqliteColumnInfo(bool IsNullable, string? DefaultValue);
}
