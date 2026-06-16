using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Domain;

public sealed class RetrievedContextItem
{
    public string SourceTitle { get; set; } = string.Empty;

    public string? SourceId { get; set; }

    public string? ExternalReference { get; set; }

    public string? FragmentId { get; set; }

    public string? Text { get; set; }

    public string? Excerpt { get; set; }

    public string? UrlOrReference { get; set; }

    public int? Rank { get; set; }

    public double? Score { get; set; }

    public string? ProviderName { get; set; }

    public string? AdapterName { get; set; }

    public RetrievedContextItemCompleteness Completeness { get; set; } = RetrievedContextItemCompleteness.Unavailable;

    public string? WarningOrLimitationNote { get; set; }
}
