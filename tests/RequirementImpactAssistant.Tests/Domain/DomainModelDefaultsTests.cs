using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Tests.Domain;

public sealed class DomainModelDefaultsTests
{
    [Fact]
    public void Analysis_UsesDraftStatusAndEmptyCollectionsByDefault()
    {
        var analysis = new Analysis();

        Assert.Equal(AnalysisStatus.Draft, analysis.Status);
        Assert.NotEqual(default, analysis.CreatedAt);
        Assert.Equal(analysis.CreatedAt, analysis.UpdatedAt);
        Assert.Empty(analysis.ContextFragments);
        Assert.Null(analysis.AiAnalysisResult);
        Assert.Null(analysis.ExpertEvaluation);
        Assert.Null(analysis.ExpertConclusion);
    }

    [Fact]
    public void ContextFragment_DefaultsToOtherType()
    {
        var fragment = new ContextFragment();

        Assert.Equal(ContextFragmentType.Other, fragment.Type);
        Assert.NotEqual(default, fragment.CreatedAt);
    }

    [Fact]
    public void AiAnalysisResult_DefaultsToNotStartedWithoutImpactMap()
    {
        var result = new AiAnalysisResult();

        Assert.Equal(AiAnalysisResultStatus.NotStarted, result.Status);
        Assert.Null(result.GeneratedAt);
        Assert.Null(result.ImpactMap);
        Assert.Equal(string.Empty, result.ErrorMessage);
    }

    [Fact]
    public void ExpertEvaluation_DefaultsToNotAssessed()
    {
        var evaluation = new ExpertEvaluation();

        Assert.Equal(ContextSufficiencyRating.NotAssessed, evaluation.ContextSufficiency);
        Assert.Equal(ResultUsefulnessRating.NotAssessed, evaluation.ResultUsefulness);
        Assert.Empty(evaluation.EvaluatedItems);
        Assert.Empty(evaluation.MissedItems);
        Assert.Empty(evaluation.Corrections);
    }

    [Fact]
    public void ExpertConclusion_DefaultsToNotSet()
    {
        var conclusion = new ExpertConclusion();

        Assert.Equal(ExpertConclusionType.NotSet, conclusion.ConclusionType);
        Assert.Null(conclusion.FixedAt);
    }

    [Fact]
    public void ImpactMap_DefaultsSingletonEntriesWithStableIds()
    {
        var impactMap = new ImpactMap();

        Assert.Equal("change-summary", impactMap.ChangeSummary.Id);
        Assert.Equal(ImpactMapItemType.ChangeSummary, impactMap.ChangeSummary.ItemType);
        Assert.Equal("preliminary-assessment", impactMap.PreliminaryAssessment.Id);
        Assert.Equal(ImpactMapItemType.PreliminaryAssessment, impactMap.PreliminaryAssessment.ItemType);
    }
}
