namespace RequirementImpactAssistant.Web.Application.Analysis.External;

public sealed record ExternalRagAdapterError(
    string Code,
    string Message,
    string? DiagnosticDetails);
