namespace RequirementImpactAssistant.Web.Application.Analysis.External;

public sealed record ExternalRagRequestMetadata(
    string EngineName,
    string? RequestedProfileName,
    IReadOnlyDictionary<string, string> SanitizedProperties);
