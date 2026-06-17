using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;
using System.Reflection;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class AnalysisInputAssemblerTests
{
    [Fact]
    public void Assemble_CreatesStableSerializableInputSnapshot()
    {
        var analysisId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var firstFragmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondFragmentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var analysis = CreateAnalysis(analysisId);
        analysis.ContextFragments.Add(new ContextFragment
        {
            Id = secondFragmentId,
            AnalysisId = analysisId,
            Type = ContextFragmentType.ApiDescription,
            Source = "API notes",
            Text = "Endpoint contract.",
            FileName = "api.md",
            CreatedAt = new DateTimeOffset(2026, 06, 13, 10, 00, 00, TimeSpan.Zero)
        });
        analysis.ContextFragments.Add(new ContextFragment
        {
            Id = firstFragmentId,
            AnalysisId = analysisId,
            Type = ContextFragmentType.Task,
            Source = "Task tracker",
            Text = "Task context.",
            CreatedAt = new DateTimeOffset(2026, 06, 13, 09, 00, 00, TimeSpan.Zero)
        });

        var request = new AnalysisInputAssembler().Assemble(analysis);

        Assert.Equal(
            "{\"analysisId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"analysis\":{\"title\":\"Gateway migration\",\"projectRequestType\":\"ApiOrIntegrationChange\",\"originalDescription\":\"Original requirement\",\"projectRequest\":\"Project request\",\"situationDescription\":\"Current situation\",\"changeSource\":\"Change source\"},\"contextFragments\":[{\"id\":\"11111111-1111-1111-1111-111111111111\",\"type\":\"Task\",\"source\":\"Task tracker\",\"text\":\"Task context.\",\"fileName\":null},{\"id\":\"22222222-2222-2222-2222-222222222222\",\"type\":\"ApiDescription\",\"source\":\"API notes\",\"text\":\"Endpoint contract.\",\"fileName\":\"api.md\"}]}",
            request.InputSnapshotJson);
        Assert.Equal(request.InputSnapshotJson, new AnalysisInputAssembler().Assemble(analysis).InputSnapshotJson);
        Assert.True(request.BoundaryNotice.IsPreliminaryAnalyticalMaterial);
        Assert.True(request.BoundaryNotice.AiDoesNotMakeManagementDecision);
    }

    [Fact]
    public void Assemble_OrdersContextFragmentsByCreatedAtThenId()
    {
        var analysisId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var laterFragmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var firstTieFragmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondTieFragmentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var sameTimestamp = new DateTimeOffset(2026, 06, 13, 09, 00, 00, TimeSpan.Zero);
        var analysis = CreateAnalysis(analysisId);

        analysis.ContextFragments.Add(CreateFragment(analysisId, laterFragmentId, sameTimestamp.AddMinutes(1)));
        analysis.ContextFragments.Add(CreateFragment(analysisId, secondTieFragmentId, sameTimestamp));
        analysis.ContextFragments.Add(CreateFragment(analysisId, firstTieFragmentId, sameTimestamp));

        var request = new AnalysisInputAssembler().Assemble(analysis);

        Assert.Collection(
            request.InputSnapshot.ContextFragments,
            fragment => Assert.Equal(firstTieFragmentId, fragment.Id),
            fragment => Assert.Equal(secondTieFragmentId, fragment.Id),
            fragment => Assert.Equal(laterFragmentId, fragment.Id));
    }

    [Fact]
    public void Assemble_DoesNotIncludeInternalStateOrPreviouslyGeneratedResults()
    {
        var analysisId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var analysis = CreateAnalysis(analysisId);
        analysis.Status = AnalysisStatus.NeedsExpertEvaluation;
        analysis.FixedAt = new DateTimeOffset(2026, 06, 13, 11, 00, 00, TimeSpan.Zero);
        analysis.AiAnalysisResult = new AiAnalysisResult
        {
            AnalysisId = analysisId,
            RawResponse = "previous AI response must not be reused",
            ErrorMessage = "previous error must not be reused"
        };
        analysis.ContextFragments.Add(new ContextFragment
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AnalysisId = analysisId,
            Type = ContextFragmentType.DocumentFragment,
            Source = "Uploaded file",
            Text = "User uploaded content.",
            FileName = "context.md",
            FilePath = "data/uploads/internal/path/context.md",
            CreatedAt = new DateTimeOffset(2026, 06, 13, 09, 00, 00, TimeSpan.Zero)
        });

        var request = new AnalysisInputAssembler().Assemble(analysis);

        Assert.DoesNotContain("status", request.InputSnapshotJson);
        Assert.DoesNotContain("createdAt", request.InputSnapshotJson);
        Assert.DoesNotContain("updatedAt", request.InputSnapshotJson);
        Assert.DoesNotContain("fixedAt", request.InputSnapshotJson);
        Assert.DoesNotContain("filePath", request.InputSnapshotJson);
        Assert.DoesNotContain("internal/path", request.InputSnapshotJson);
        Assert.DoesNotContain("previous AI response", request.InputSnapshotJson);
        Assert.DoesNotContain("previous error", request.InputSnapshotJson);
        Assert.Contains("User uploaded content.", request.InputSnapshotJson);
        Assert.Contains("context.md", request.InputSnapshotJson);
    }

    [Fact]
    public void Assemble_IncludesOnlyContextForCurrentAnalysis()
    {
        var analysisId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var otherAnalysisId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var analysis = CreateAnalysis(analysisId);
        analysis.ContextFragments.Add(new ContextFragment
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AnalysisId = analysisId,
            Type = ContextFragmentType.Task,
            Source = "Current source",
            Text = "Current analysis context.",
            CreatedAt = new DateTimeOffset(2026, 06, 13, 09, 00, 00, TimeSpan.Zero)
        });
        analysis.ContextFragments.Add(new ContextFragment
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            AnalysisId = otherAnalysisId,
            Type = ContextFragmentType.Comment,
            Source = "Foreign source",
            Text = "Foreign analysis context.",
            CreatedAt = new DateTimeOffset(2026, 06, 13, 10, 00, 00, TimeSpan.Zero)
        });

        var request = new AnalysisInputAssembler().Assemble(analysis);

        var fragment = Assert.Single(request.InputSnapshot.ContextFragments);
        Assert.Equal("Current analysis context.", fragment.Text);
        Assert.DoesNotContain("Foreign analysis context.", request.InputSnapshotJson);
        Assert.DoesNotContain(otherAnalysisId.ToString(), request.InputSnapshotJson);
    }

    [Fact]
    public void Request_DescribesExpectedImpactMapSectionsWithoutProviderSchema()
    {
        var request = new AnalysisInputAssembler().Assemble(CreateAnalysis(Guid.NewGuid()));

        Assert.Collection(
            request.ExpectedResult.Sections,
            section => AssertSection(section, "changeSummary", ImpactMapItemType.ChangeSummary, isCollection: false),
            section => AssertSection(section, "affectedRequirements", ImpactMapItemType.AffectedRequirement, isCollection: true),
            section => AssertSection(section, "affectedTasks", ImpactMapItemType.AffectedTask, isCollection: true),
            section => AssertSection(section, "affectedProjectDecisions", ImpactMapItemType.AffectedProjectDecision, isCollection: true),
            section => AssertSection(section, "affectedApiInterfacesDocumentsTests", ImpactMapItemType.AffectedApiInterfaceDocumentTest, isCollection: true),
            section => AssertSection(section, "affectedArchitecturalConstraints", ImpactMapItemType.AffectedArchitecturalConstraint, isCollection: true),
            section => AssertSection(section, "affectedOrganizationalContextItems", ImpactMapItemType.AffectedOrganizationalContextItem, isCollection: true),
            section => AssertSection(section, "contradictions", ImpactMapItemType.Contradiction, isCollection: true),
            section => AssertSection(section, "missingInformation", ImpactMapItemType.MissingInformation, isCollection: true),
            section => AssertSection(section, "clarificationQuestions", ImpactMapItemType.ClarificationQuestion, isCollection: true),
            section => AssertSection(section, "risks", ImpactMapItemType.Risk, isCollection: true),
            section => AssertSection(section, "optionsForExpertReview", ImpactMapItemType.OptionForExpertReview, isCollection: true),
            section => AssertSection(section, "preliminaryAssessment", ImpactMapItemType.PreliminaryAssessment, isCollection: false));
    }

    [Fact]
    public void ContractTypes_DoNotDependOnConcreteProviderSdk()
    {
        var contractTypes = new[]
        {
            typeof(IAiAnalysisEngine),
            typeof(AiAnalysisRequest),
            typeof(AiAnalysisResponse),
            typeof(AnalysisInputSnapshot),
            typeof(AnalysisInputFields),
            typeof(AnalysisContextFragmentSnapshot),
            typeof(ExpectedAnalysisResultStructure),
            typeof(ExpectedAnalysisResultSection),
            typeof(AnalysisBoundaryNotice)
        };

        foreach (var type in contractTypes)
        {
            Assert.DoesNotContain("OpenAI", type.FullName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DeepSeek", type.FullName, StringComparison.OrdinalIgnoreCase);

            foreach (var memberType in GetPublicContractMemberTypes(type))
            {
                Assert.DoesNotContain("OpenAI", memberType.FullName, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("DeepSeek", memberType.FullName, StringComparison.OrdinalIgnoreCase);
                Assert.NotEqual("Azure.AI.OpenAI", memberType.Namespace);
            }
        }
    }

    private static Analysis CreateAnalysis(Guid analysisId) =>
        new()
        {
            Id = analysisId,
            Title = "Gateway migration",
            ProjectRequestType = ProjectRequestType.ApiOrIntegrationChange,
            Status = AnalysisStatus.ReadyForAnalysis,
            OriginalDescription = "Original requirement",
            ProjectRequest = "Project request",
            SituationDescription = "Current situation",
            ChangeSource = "Change source",
            CreatedAt = new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 06, 13, 08, 30, 00, TimeSpan.Zero)
        };

    private static ContextFragment CreateFragment(
        Guid analysisId,
        Guid fragmentId,
        DateTimeOffset createdAt) =>
        new()
        {
            Id = fragmentId,
            AnalysisId = analysisId,
            Type = ContextFragmentType.Comment,
            Source = $"Source {fragmentId:N}",
            Text = $"Context {fragmentId:N}",
            CreatedAt = createdAt
        };

    private static void AssertSection(
        ExpectedAnalysisResultSection section,
        string key,
        ImpactMapItemType itemType,
        bool isCollection)
    {
        Assert.Equal(key, section.Key);
        Assert.Equal(itemType.ToString(), section.ItemType);
        Assert.Equal(isCollection, section.IsCollection);
        Assert.True(section.AllowsRelatedContextFragmentIds);
    }

    private static IEnumerable<Type> GetPublicContractMemberTypes(Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            yield return UnwrapType(property.PropertyType);
        }

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type))
        {
            yield return UnwrapType(method.ReturnType);

            foreach (var parameter in method.GetParameters())
            {
                yield return UnwrapType(parameter.ParameterType);
            }
        }
    }

    private static Type UnwrapType(Type type)
    {
        if (type.IsGenericType)
        {
            return type.GetGenericArguments()[0];
        }

        return type;
    }
}
