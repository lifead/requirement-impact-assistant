using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Impact;
using System.Text.Json;

namespace RequirementImpactAssistant.Web.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly ValueConverter<List<Guid>, string> GuidListJsonConverter = new(
        value => JsonSerializer.Serialize(value, JsonSerializerOptions),
        value => string.IsNullOrWhiteSpace(value)
            ? new List<Guid>()
            : JsonSerializer.Deserialize<List<Guid>>(value, JsonSerializerOptions) ?? new List<Guid>());

    private static readonly ValueComparer<List<Guid>> GuidListValueComparer = new(
        (left, right) => left != null && right != null && left.SequenceEqual(right),
        value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        value => value.ToList());

    public DbSet<Analysis> Analyses => Set<Analysis>();

    public DbSet<ContextFragment> ContextFragments => Set<ContextFragment>();

    public DbSet<AiAnalysisResult> AiAnalysisResults => Set<AiAnalysisResult>();

    public DbSet<ExpertEvaluation> ExpertEvaluations => Set<ExpertEvaluation>();

    public DbSet<ExpertEvaluatedItem> ExpertEvaluatedItems => Set<ExpertEvaluatedItem>();

    public DbSet<ExpertMissedItem> ExpertMissedItems => Set<ExpertMissedItem>();

    public DbSet<ExpertCorrection> ExpertCorrections => Set<ExpertCorrection>();

    public DbSet<ExpertConclusion> ExpertConclusions => Set<ExpertConclusion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureAnalysis(modelBuilder.Entity<Analysis>());
        ConfigureContextFragment(modelBuilder.Entity<ContextFragment>());
        ConfigureAiAnalysisResult(modelBuilder.Entity<AiAnalysisResult>());
        ConfigureExpertEvaluation(modelBuilder.Entity<ExpertEvaluation>());
        ConfigureExpertEvaluatedItem(modelBuilder.Entity<ExpertEvaluatedItem>());
        ConfigureExpertMissedItem(modelBuilder.Entity<ExpertMissedItem>());
        ConfigureExpertCorrection(modelBuilder.Entity<ExpertCorrection>());
        ConfigureExpertConclusion(modelBuilder.Entity<ExpertConclusion>());
    }

    private static void ConfigureAnalysis(EntityTypeBuilder<Analysis> entity)
    {
        entity.ToTable("Analyses");

        entity.HasKey(analysis => analysis.Id);

        entity.Property(analysis => analysis.Title)
            .HasMaxLength(300)
            .IsRequired();

        entity.Property(analysis => analysis.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(analysis => analysis.OriginalDescription)
            .IsRequired();

        entity.Property(analysis => analysis.ProjectRequest)
            .IsRequired();

        entity.Property(analysis => analysis.SituationDescription)
            .IsRequired();

        entity.Property(analysis => analysis.ChangeSource)
            .HasMaxLength(500)
            .IsRequired();

        entity.HasMany(analysis => analysis.ContextFragments)
            .WithOne()
            .HasForeignKey(fragment => fragment.AnalysisId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(analysis => analysis.AiAnalysisResult)
            .WithOne()
            .HasForeignKey<AiAnalysisResult>(result => result.AnalysisId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(analysis => analysis.ExpertEvaluation)
            .WithOne()
            .HasForeignKey<ExpertEvaluation>(evaluation => evaluation.AnalysisId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(analysis => analysis.ExpertConclusion)
            .WithOne()
            .HasForeignKey<ExpertConclusion>(conclusion => conclusion.AnalysisId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureContextFragment(EntityTypeBuilder<ContextFragment> entity)
    {
        entity.ToTable("ContextFragments");

        entity.HasKey(fragment => fragment.Id);

        entity.HasIndex(fragment => fragment.AnalysisId);

        entity.Property(fragment => fragment.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(fragment => fragment.Source)
            .HasMaxLength(500)
            .IsRequired();

        entity.Property(fragment => fragment.Text)
            .IsRequired();

        entity.Property(fragment => fragment.FileName)
            .HasMaxLength(260);

        entity.Property(fragment => fragment.FilePath)
            .HasMaxLength(1_000);
    }

    private static void ConfigureAiAnalysisResult(EntityTypeBuilder<AiAnalysisResult> entity)
    {
        entity.ToTable("AiAnalysisResults");

        entity.HasKey(result => result.Id);

        entity.HasIndex(result => result.AnalysisId)
            .IsUnique();

        entity.Property(result => result.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(result => result.EngineName)
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(result => result.ProviderName)
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(result => result.ModelName)
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(result => result.PromptVersion)
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(result => result.InputSnapshot)
            .IsRequired();

        entity.Property(result => result.RawResponse)
            .IsRequired();

        entity.Property(result => result.ErrorMessage)
            .IsRequired();

        entity.OwnsOne(result => result.ImpactMap, ConfigureImpactMap);
    }

    private static void ConfigureImpactMap(
        OwnedNavigationBuilder<AiAnalysisResult, ImpactMap> impactMap)
    {
        impactMap.ToTable("ImpactMaps");

        impactMap.WithOwner()
            .HasForeignKey("AiAnalysisResultId");

        impactMap.Property<Guid>("AiAnalysisResultId");

        impactMap.HasKey("AiAnalysisResultId");

        impactMap.OwnsOne(map => map.ChangeSummary, item =>
            ConfigureImpactMapSingletonItem(item, "ChangeSummary"));

        impactMap.OwnsOne(map => map.PreliminaryAssessment, item =>
            ConfigureImpactMapSingletonItem(item, "PreliminaryAssessment"));

        ConfigureImpactMapCollection(
            impactMap.OwnsMany(map => map.AffectedRequirements),
            "ImpactMapAffectedRequirements");
        ConfigureImpactMapCollection(
            impactMap.OwnsMany(map => map.AffectedTasks),
            "ImpactMapAffectedTasks");
        ConfigureImpactMapCollection(
            impactMap.OwnsMany(map => map.AffectedProjectDecisions),
            "ImpactMapAffectedProjectDecisions");
        ConfigureImpactMapCollection(
            impactMap.OwnsMany(map => map.AffectedApiInterfacesDocumentsTests),
            "ImpactMapAffectedApiInterfacesDocumentsTests");
        ConfigureImpactMapCollection(
            impactMap.OwnsMany(map => map.AffectedArchitecturalConstraints),
            "ImpactMapAffectedArchitecturalConstraints");
        ConfigureImpactMapCollection(
            impactMap.OwnsMany(map => map.AffectedOrganizationalContextItems),
            "ImpactMapAffectedOrganizationalContextItems");
        ConfigureImpactMapCollection(
            impactMap.OwnsMany(map => map.Contradictions),
            "ImpactMapContradictions");
        ConfigureImpactMapCollection(
            impactMap.OwnsMany(map => map.MissingInformation),
            "ImpactMapMissingInformation");
        ConfigureImpactMapCollection(
            impactMap.OwnsMany(map => map.ClarificationQuestions),
            "ImpactMapClarificationQuestions");
        ConfigureImpactMapCollection(
            impactMap.OwnsMany(map => map.Risks),
            "ImpactMapRisks");
        ConfigureImpactMapCollection(
            impactMap.OwnsMany(map => map.OptionsForExpertReview),
            "ImpactMapOptionsForExpertReview");
    }

    private static void ConfigureImpactMapSingletonItem<TOwner>(
        OwnedNavigationBuilder<TOwner, ImpactMapItem> item,
        string columnPrefix)
        where TOwner : class
    {
        item.Property(mapItem => mapItem.Id)
            .HasColumnName($"{columnPrefix}Id")
            .HasMaxLength(150)
            .IsRequired();

        item.Property(mapItem => mapItem.ItemType)
            .HasColumnName($"{columnPrefix}ItemType")
            .HasConversion<string>()
            .HasMaxLength(80)
            .IsRequired();

        item.Property(mapItem => mapItem.Title)
            .HasColumnName($"{columnPrefix}Title")
            .HasMaxLength(300)
            .IsRequired();

        item.Property(mapItem => mapItem.Description)
            .HasColumnName($"{columnPrefix}Description")
            .IsRequired();

        item.Property(mapItem => mapItem.Severity)
            .HasColumnName($"{columnPrefix}Severity")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        ConfigureRelatedContextFragmentIds(
            item.Property(mapItem => mapItem.RelatedContextFragmentIds)
                .HasColumnName($"{columnPrefix}RelatedContextFragmentIds"));

        item.Property(mapItem => mapItem.Notes)
            .HasColumnName($"{columnPrefix}Notes")
            .IsRequired();
    }

    private static void ConfigureImpactMapCollection(
        OwnedNavigationBuilder<ImpactMap, ImpactMapItem> items,
        string tableName)
    {
        items.ToTable(tableName);

        items.WithOwner()
            .HasForeignKey("AiAnalysisResultId");

        items.Property<Guid>("AiAnalysisResultId");

        items.Property<int>("Ordinal");

        items.HasKey("AiAnalysisResultId", nameof(ImpactMapItem.Id));

        items.Property(mapItem => mapItem.Id)
            .HasMaxLength(150)
            .IsRequired();

        items.Property(mapItem => mapItem.ItemType)
            .HasConversion<string>()
            .HasMaxLength(80)
            .IsRequired();

        items.Property(mapItem => mapItem.Title)
            .HasMaxLength(300)
            .IsRequired();

        items.Property(mapItem => mapItem.Description)
            .IsRequired();

        items.Property(mapItem => mapItem.Severity)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        ConfigureRelatedContextFragmentIds(
            items.Property(mapItem => mapItem.RelatedContextFragmentIds));

        items.Property(mapItem => mapItem.Notes)
            .IsRequired();
    }

    private static void ConfigureExpertEvaluation(EntityTypeBuilder<ExpertEvaluation> entity)
    {
        entity.ToTable("ExpertEvaluations");

        entity.HasKey(evaluation => evaluation.Id);

        entity.HasIndex(evaluation => evaluation.AnalysisId)
            .IsUnique();

        entity.Property(evaluation => evaluation.ContextSufficiency)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(evaluation => evaluation.ResultUsefulness)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(evaluation => evaluation.GeneralComment)
            .IsRequired();

        entity.HasMany(evaluation => evaluation.EvaluatedItems)
            .WithOne()
            .HasForeignKey("ExpertEvaluationId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(evaluation => evaluation.MissedItems)
            .WithOne()
            .HasForeignKey("ExpertEvaluationId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(evaluation => evaluation.Corrections)
            .WithOne()
            .HasForeignKey("ExpertEvaluationId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureExpertEvaluatedItem(EntityTypeBuilder<ExpertEvaluatedItem> entity)
    {
        entity.ToTable("ExpertEvaluatedItems");

        entity.HasKey(item => item.Id);

        entity.Property<Guid>("ExpertEvaluationId");

        entity.HasIndex("ExpertEvaluationId");

        entity.Property(item => item.TargetType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(item => item.TargetId)
            .HasMaxLength(150)
            .IsRequired();

        entity.Property(item => item.Mark)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(item => item.Comment)
            .IsRequired();

        entity.Property(item => item.CorrectionText)
            .IsRequired();
    }

    private static void ConfigureExpertMissedItem(EntityTypeBuilder<ExpertMissedItem> entity)
    {
        entity.ToTable("ExpertMissedItems");

        entity.HasKey(item => item.Id);

        entity.Property<Guid>("ExpertEvaluationId");

        entity.HasIndex("ExpertEvaluationId");

        entity.Property(item => item.ItemType)
            .HasConversion<string>()
            .HasMaxLength(80)
            .IsRequired();

        entity.Property(item => item.Title)
            .HasMaxLength(300)
            .IsRequired();

        entity.Property(item => item.Description)
            .IsRequired();

        entity.Property(item => item.Severity)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(item => item.Comment)
            .IsRequired();
    }

    private static void ConfigureExpertCorrection(EntityTypeBuilder<ExpertCorrection> entity)
    {
        entity.ToTable("ExpertCorrections");

        entity.HasKey(correction => correction.Id);

        entity.Property<Guid>("ExpertEvaluationId");

        entity.HasIndex("ExpertEvaluationId");

        entity.Property(correction => correction.TargetType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(correction => correction.TargetId)
            .HasMaxLength(150)
            .IsRequired();

        entity.Property(correction => correction.ItemType)
            .HasConversion<string>()
            .HasMaxLength(80)
            .IsRequired();

        entity.Property(correction => correction.Text)
            .IsRequired();

        entity.Property(correction => correction.Comment)
            .IsRequired();
    }

    private static void ConfigureExpertConclusion(EntityTypeBuilder<ExpertConclusion> entity)
    {
        entity.ToTable("ExpertConclusions");

        entity.HasKey(conclusion => conclusion.Id);

        entity.HasIndex(conclusion => conclusion.AnalysisId)
            .IsUnique();

        entity.Property(conclusion => conclusion.ConclusionType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(conclusion => conclusion.Comment)
            .IsRequired();

        entity.Property(conclusion => conclusion.Rationale)
            .IsRequired();
    }

    private static void ConfigureRelatedContextFragmentIds(
        PropertyBuilder<List<Guid>> propertyBuilder)
    {
        propertyBuilder
            .HasConversion(GuidListJsonConverter)
            .Metadata.SetValueComparer(GuidListValueComparer);
    }
}
