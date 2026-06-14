namespace RequirementImpactAssistant.Web.Application.Analysis.External;

public interface IExternalRagAdapter
{
    Task<ExternalRagAdapterResponse> AnalyzeAsync(
        ExternalRagAdapterRequest request,
        CancellationToken cancellationToken = default);
}
