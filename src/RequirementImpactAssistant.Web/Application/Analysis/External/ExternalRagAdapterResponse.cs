using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Web.Application.Analysis.External;

public sealed record ExternalRagAdapterResponse(
    ExternalRagAdapterResponseStatus Status,
    ImpactMap? ImpactMap,
    ExternalRagAdapterResponseMetadata Metadata,
    RetrievedContextState RetrievedContextState,
    IReadOnlyList<RetrievedContextItem> RetrievedContextItems,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ExternalRagAdapterError> Errors,
    string? SanitizedDiagnosticSnapshot);
