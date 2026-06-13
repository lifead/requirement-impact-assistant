using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
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

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        return new ApplicationDbContext(options);
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
}
