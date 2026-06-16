using RequirementImpactAssistant.Web.Application.Analysis.External;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Application.Analysis;

public sealed class ExternalRagAnalysisEngine : IAiAnalysisEngine
{
    public const string EngineName = nameof(ExternalRagAnalysisEngine);

    private const string AdapterUnavailableMessage =
        "External AI/RAG adapter is not configured for this analysis mode.";

    private readonly IExternalRagAdapter? adapter;

    public ExternalRagAnalysisEngine(IExternalRagAdapter? adapter = null)
    {
        this.adapter = adapter;
    }

    public async Task<AiAnalysisResponse> AnalyzeAsync(
        AiAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (adapter is null)
        {
            return CreateUnavailableResponse(request);
        }

        var adapterRequest = CreateAdapterRequest(request);

        try
        {
            var adapterResponse = await adapter
                .AnalyzeAsync(adapterRequest, cancellationToken)
                .ConfigureAwait(false);

            return MapAdapterResponse(adapterResponse, request.BoundaryNotice, adapterRequest);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CreateAdapterFailureResponse(request, exception);
        }
    }

    private static ExternalRagAdapterRequest CreateAdapterRequest(AiAnalysisRequest request)
    {
        var manualContext = CreateManualContext(request.InputSnapshot.ContextFragments);

        return new ExternalRagAdapterRequest(
            CorrelationId: request.InputSnapshot.AnalysisId,
            InputSnapshot: request.InputSnapshot,
            ManualContext: manualContext,
            CanForwardManualContextToExternalAiOrRag: manualContext is not null,
            ExpectedResult: request.ExpectedResult,
            BoundaryNotice: request.BoundaryNotice,
            ExecutionMetadata: new ExternalRagRequestMetadata(
                EngineName: EngineName,
                RequestedProfileName: null,
                SanitizedProperties: new Dictionary<string, string>()));
    }

    private static ExternalRagManualContextBlock? CreateManualContext(
        IReadOnlyList<AnalysisContextFragmentSnapshot> contextFragments)
    {
        if (contextFragments.Count == 0)
        {
            return null;
        }

        var combinedText = string.Join(
            Environment.NewLine + Environment.NewLine,
            contextFragments.Select(fragment => fragment.Text));

        return new ExternalRagManualContextBlock(
            ContextFragments: contextFragments,
            CombinedText: combinedText);
    }

    private static AiAnalysisResponse MapAdapterResponse(
        ExternalRagAdapterResponse adapterResponse,
        AnalysisBoundaryNotice boundaryNotice,
        ExternalRagAdapterRequest adapterRequest)
    {
        var diagnostics = CreateDiagnostics(adapterResponse.Warnings, adapterResponse.Errors);
        var status = MapStatus(adapterResponse.Status);
        var impactMap = adapterResponse.ImpactMap;

        if (status is not AiAnalysisResponseStatus.Failed && impactMap is null)
        {
            diagnostics.Add("External adapter response is invalid: impact map is missing.");
            status = AiAnalysisResponseStatus.Failed;
        }

        return new AiAnalysisResponse(
            Status: status,
            ImpactMap: status is AiAnalysisResponseStatus.Failed ? null : impactMap,
            RawResponse: adapterResponse.SanitizedDiagnosticSnapshot ?? string.Empty,
            Errors: diagnostics,
            BoundaryNotice: boundaryNotice,
            ResultMetadata: CreateMetadata(adapterResponse, adapterRequest));
    }

    private static AiAnalysisResultMetadata CreateMetadata(
        ExternalRagAdapterResponse adapterResponse,
        ExternalRagAdapterRequest adapterRequest) =>
        new()
        {
            AnalysisMode = AnalysisMode.ExternalRag,
            EngineName = EngineName,
            ProviderName = NormalizeOptional(adapterResponse.Metadata.ProviderName),
            AdapterName = NormalizeOptional(adapterResponse.Metadata.AdapterName),
            ModelWorkflowProfileName = CreateModelWorkflowProfileName(adapterResponse.Metadata),
            RetrievedContextState = adapterResponse.RetrievedContextState,
            RetrievedContextItems = adapterResponse.RetrievedContextItems.ToList(),
            Warnings = NormalizeMessages(adapterResponse.Warnings),
            ManualContextForwardedToExternalAiOrRag = adapterRequest.CanForwardManualContextToExternalAiOrRag
        };

    private static AiAnalysisResponse CreateUnavailableResponse(AiAnalysisRequest request) =>
        new(
            Status: AiAnalysisResponseStatus.Failed,
            ImpactMap: null,
            RawResponse: string.Empty,
            Errors: [AdapterUnavailableMessage],
            BoundaryNotice: request.BoundaryNotice,
            ResultMetadata: new AiAnalysisResultMetadata
            {
                AnalysisMode = AnalysisMode.ExternalRag,
                EngineName = EngineName,
                RetrievedContextState = RetrievedContextState.Unavailable,
                RetrievedContextItems = [],
                Warnings = [AdapterUnavailableMessage],
                ManualContextForwardedToExternalAiOrRag = false
            });

    private static AiAnalysisResponse CreateAdapterFailureResponse(
        AiAnalysisRequest request,
        Exception exception)
    {
        var diagnostic = $"External adapter failed before returning an analytical result. Failure type: {exception.GetType().Name}.";

        return new AiAnalysisResponse(
            Status: AiAnalysisResponseStatus.Failed,
            ImpactMap: null,
            RawResponse: string.Empty,
            Errors: [diagnostic],
            BoundaryNotice: request.BoundaryNotice,
            ResultMetadata: new AiAnalysisResultMetadata
            {
                AnalysisMode = AnalysisMode.ExternalRag,
                EngineName = EngineName,
                RetrievedContextState = RetrievedContextState.Unavailable,
                RetrievedContextItems = [],
                Warnings = [diagnostic],
                ManualContextForwardedToExternalAiOrRag = false
            });
    }

    private static AiAnalysisResponseStatus MapStatus(ExternalRagAdapterResponseStatus status) =>
        status switch
        {
            ExternalRagAdapterResponseStatus.Completed => AiAnalysisResponseStatus.Succeeded,
            ExternalRagAdapterResponseStatus.CompletedWithWarnings => AiAnalysisResponseStatus.Partial,
            ExternalRagAdapterResponseStatus.Partial => AiAnalysisResponseStatus.Partial,
            ExternalRagAdapterResponseStatus.Failed => AiAnalysisResponseStatus.Failed,
            _ => AiAnalysisResponseStatus.Failed
        };

    private static List<string> CreateDiagnostics(
        IReadOnlyList<string> warnings,
        IReadOnlyList<ExternalRagAdapterError> errors)
    {
        var diagnostics = NormalizeMessages(warnings);

        diagnostics.AddRange(errors
            .Where(error => !string.IsNullOrWhiteSpace(error.Message))
            .Select(error => FormatError(error)));

        return diagnostics;
    }

    private static string FormatError(ExternalRagAdapterError error)
    {
        var code = NormalizeOptional(error.Code);
        var diagnosticDetails = NormalizeOptional(error.DiagnosticDetails);
        var message = NormalizeOptional(error.Message) ?? "External adapter returned an error.";
        var formatted = code is null
            ? message
            : $"{code}: {message}";

        return diagnosticDetails is null
            ? formatted
            : $"{formatted} {diagnosticDetails}";
    }

    private static string? CreateModelWorkflowProfileName(ExternalRagAdapterResponseMetadata metadata)
    {
        var values = new[]
            {
                metadata.ModelName,
                metadata.WorkflowName,
                metadata.ProfileName
            }
            .Select(NormalizeOptional)
            .Where(value => value is not null)
            .ToArray();

        return values.Length == 0
            ? null
            : string.Join(" / ", values);
    }

    private static List<string> NormalizeMessages(IReadOnlyList<string> messages) =>
        messages
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToList();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value;
}
