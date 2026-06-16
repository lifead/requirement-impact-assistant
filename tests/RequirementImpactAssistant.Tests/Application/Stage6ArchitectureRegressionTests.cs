using System.Reflection;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.External;
using RequirementImpactAssistant.Web.Application.Analysis.External.Dify;
using RequirementImpactAssistant.Web.Application.Analysis.Llm;
using RequirementImpactAssistant.Web.Application.Export;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Pages.Analyses;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class Stage6ArchitectureRegressionTests
{
    private const string WebProjectName = "RequirementImpactAssistant.Web";
    private const string DifyNamespace = "RequirementImpactAssistant.Web.Application.Analysis.External.Dify";
    private const string ExportNamespace = "RequirementImpactAssistant.Web.Application.Export";
    private const string PagesNamespace = "RequirementImpactAssistant.Web.Pages";

    private static readonly Assembly WebAssembly = typeof(IAiAnalysisEngine).Assembly;

    [Fact]
    public void PageModels_DoNotDependOnEnginesAdaptersProvidersDifyContractsOrNetworkClients()
    {
        var forbiddenRootTypes = new[]
        {
            typeof(IAiAnalysisEngine),
            typeof(IAiAnalysisEngineSelector),
            typeof(IExternalRagAdapter),
            typeof(ExternalRagAnalysisEngine),
            typeof(ILlmProvider),
            typeof(HttpClient),
            typeof(HttpMessageHandler),
            typeof(HttpRequestMessage),
            typeof(HttpResponseMessage)
        };

        var violations = WebAssembly
            .GetTypes()
            .Where(type => type.IsAssignableTo(typeof(PageModel)))
            .SelectMany(type => GetReferencedTypes(type)
                .Where(referencedType =>
                    IsDifySpecificType(referencedType) ||
                    IsExternalAdapterContractType(referencedType) ||
                    IsProviderPayloadType(referencedType) ||
                    forbiddenRootTypes.Any(forbiddenType => forbiddenType.IsAssignableFrom(referencedType)))
                .Select(referencedType => $"{type.FullName} -> {referencedType.FullName}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void PageSource_DoesNotReferenceEnginesAdaptersProviderPayloadsDifySecretsOrExternalNetwork()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenTokens = new[]
        {
            "IAiAnalysisEngine",
            "IAiAnalysisEngineSelector",
            "IExternalRagAdapter",
            "ExternalRagAdapter",
            "RequirementImpactAssistant.Web.Application.Analysis.External",
            "DifyExternal",
            "DifyWorkflow",
            "DifyRetrieved",
            "DifyImpact",
            "DifyManual",
            "workflow_run_id",
            "workflow_id",
            "task_id",
            "response_mode",
            "manual_context",
            "response_shape",
            "SanitizedDiagnosticSnapshot",
            "LlmProviderRequest",
            "LlmProviderResponse",
            "ILlmProvider",
            "DeepSeek",
            "HttpClient",
            "IHttpClientFactory",
            "HttpRequestMessage",
            "HttpResponseMessage",
            ".SendAsync(",
            ".GetStringAsync(",
            ".PostAsync(",
            "Authorization",
            "Bearer",
            "ApiKey",
            "Endpoint"
        };

        var violations = EnumeratePageSourceFiles(repositoryRoot)
            .SelectMany(file => FindForbiddenTokens(repositoryRoot, file, forbiddenTokens))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ReviewAnalysisModeParser_AcceptsOnlyNamedModesAndDoesNotUseEnumOrNumericParsing()
    {
        var repositoryRoot = FindRepositoryRoot();
        var file = Path.Combine(
            repositoryRoot,
            "src",
            WebProjectName,
            "Pages",
            "Analyses",
            "Review.cshtml.cs");
        var source = File.ReadAllText(file);

        Assert.Contains("nameof(AnalysisModeEnum.DirectLlm) => AnalysisModeEnum.DirectLlm", source, StringComparison.Ordinal);
        Assert.Contains("nameof(AnalysisModeEnum.ExternalRag) => AnalysisModeEnum.ExternalRag", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Enum.Parse", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Enum.TryParse", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Convert.ToInt32", source, StringComparison.Ordinal);
        Assert.DoesNotContain("int.Parse", source, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"0\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"1\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailsPageSource_UsesPersistedMetadataAndRetrievedContextWithoutExternalCallsOrRawResponseParsing()
    {
        var repositoryRoot = FindRepositoryRoot();
        var detailsModelFile = Path.Combine(
            repositoryRoot,
            "src",
            WebProjectName,
            "Pages",
            "Analyses",
            "Details.cshtml.cs");
        var detailsViewFile = Path.Combine(
            repositoryRoot,
            "src",
            WebProjectName,
            "Pages",
            "Analyses",
            "Details.cshtml");
        var detailsModelSource = File.ReadAllText(detailsModelFile);
        var detailsViewSource = File.ReadAllText(detailsViewFile);
        var combinedSource = detailsModelSource + detailsViewSource;
        var forbiddenTokens = new[]
        {
            "JsonDocument.Parse",
            "JsonSerializer.Deserialize",
            "JsonNode.Parse",
            "RawResponse.Deserialize",
            "RawResponse.Split",
            "RawResponse.Contains",
            "RawResponse.IndexOf",
            "RawResponse.AsSpan",
            "RawResponse[",
            "IAnalysisExecutionService",
            "IAiAnalysisEngine",
            "IAiAnalysisEngineSelector",
            "IExternalRagAdapter",
            "ExternalRagAdapter",
            "DifyExternal",
            "DifyWorkflow",
            "LlmProviderRequest",
            "LlmProviderResponse",
            "HttpClient",
            "IHttpClientFactory",
            ".SendAsync(",
            ".GetStringAsync(",
            ".PostAsync("
        };

        Assert.Contains(".ThenInclude(result => result!.Metadata.RetrievedContextItems)", detailsModelSource, StringComparison.Ordinal);
        Assert.Contains("metadata.RetrievedContextItems", detailsModelSource, StringComparison.Ordinal);
        Assert.Contains("@aiResult.RawResponse", detailsViewSource, StringComparison.Ordinal);

        var violations = forbiddenTokens
            .Where(token => combinedSource.Contains(token, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void DirectLlmDefaultPath_RemainsDefaultAndDoesNotDependOnExternalRagAdapterOrDify()
    {
        var repositoryRoot = FindRepositoryRoot();
        var executionServiceSource = ReadProductionFile(repositoryRoot, "Application", "Analysis", "AnalysisExecutionService.cs");
        var serviceCollectionSource = ReadProductionFile(repositoryRoot, "Extensions", "ServiceCollectionExtensions.cs");
        var directLlmSource = ReadProductionFile(repositoryRoot, "Application", "Analysis", "DirectLlmAnalysisEngine.cs");
        var forbiddenDirectLlmTokens = new[]
        {
            "RequirementImpactAssistant.Web.Application.Analysis.External",
            "IExternalRagAdapter",
            "ExternalRagAdapter",
            "ExternalRagAnalysisEngine",
            "Dify",
            "dify"
        };

        Assert.Contains("RunAsync(analysisId, AnalysisMode.DirectLlm, cancellationToken)", executionServiceSource, StringComparison.Ordinal);
        Assert.Contains("AddScoped<IAiAnalysisEngine>(serviceProvider =>", serviceCollectionSource, StringComparison.Ordinal);
        Assert.Contains("GetRequiredService<DirectLlmAnalysisEngine>()", serviceCollectionSource, StringComparison.Ordinal);

        var referencedTypeViolations = GetReferencedTypes(typeof(DirectLlmAnalysisEngine))
            .Where(referencedType =>
                IsDifySpecificType(referencedType) ||
                IsExternalAdapterContractType(referencedType) ||
                typeof(IExternalRagAdapter).IsAssignableFrom(referencedType))
            .Select(referencedType => $"{typeof(DirectLlmAnalysisEngine).FullName} -> {referencedType.FullName}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        var sourceViolations = forbiddenDirectLlmTokens
            .Where(token => directLlmSource.Contains(token, StringComparison.Ordinal))
            .Select(token => $"DirectLlmAnalysisEngine.cs contains {token}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(referencedTypeViolations);
        Assert.Empty(sourceViolations);
    }

    [Fact]
    public void ExportTypesAndHandlers_DoNotCallAnalysisEnginesAdaptersProvidersOrExternalNetwork()
    {
        var forbiddenRootTypes = new[]
        {
            typeof(IAiAnalysisEngine),
            typeof(IAiAnalysisEngineSelector),
            typeof(IAnalysisExecutionService),
            typeof(IExternalRagAdapter),
            typeof(ExternalRagAnalysisEngine),
            typeof(ILlmProvider),
            typeof(PageModel),
            typeof(HttpClient),
            typeof(HttpMessageHandler),
            typeof(HttpRequestMessage),
            typeof(HttpResponseMessage)
        };

        var typeViolations = WebAssembly
            .GetTypes()
            .Where(IsExportType)
            .SelectMany(type => GetReferencedTypes(type)
                .Where(referencedType =>
                    IsDifySpecificType(referencedType) ||
                    IsExternalAdapterContractType(referencedType) ||
                    IsProviderPayloadType(referencedType) ||
                    IsPageType(referencedType) ||
                    forbiddenRootTypes.Any(forbiddenType => forbiddenType.IsAssignableFrom(referencedType)))
                .Select(referencedType => $"{type.FullName} -> {referencedType.FullName}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(typeViolations);

        var repositoryRoot = FindRepositoryRoot();
        var detailsModelSource = ReadProductionFile(repositoryRoot, "Pages", "Analyses", "Details.cshtml.cs");
        var exportHandlerSource = GetSourceBetween(
            detailsModelSource,
            "public async Task<IActionResult> OnGetExportMarkdownAsync(Guid id)",
            "public async Task<IActionResult> OnPostAddContextFragmentAsync(Guid id)");
        var detailsViewSource = ReadProductionFile(repositoryRoot, "Pages", "Analyses", "Details.cshtml");
        var forbiddenHandlerTokens = new[]
        {
            "IAnalysisExecutionService",
            "AnalysisExecutionService",
            "RunAsync(",
            "AnalyzeAsync(",
            "IAiAnalysisEngine",
            "IAiAnalysisEngineSelector",
            "IExternalRagAdapter",
            "ExternalRagAdapter",
            "LlmProvider",
            "Dify",
            "HttpClient",
            "IHttpClientFactory",
            ".SendAsync(",
            ".GetStringAsync(",
            ".PostAsync("
        };

        Assert.Contains("new AnalysisMarkdownExportService(dbContext)", exportHandlerSource, StringComparison.Ordinal);
        Assert.Contains("new AnalysisJsonExportService(dbContext)", exportHandlerSource, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"ExportJson\"", detailsViewSource, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"ExportMarkdown\"", detailsViewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OnPostExport", detailsModelSource, StringComparison.Ordinal);

        var sourceViolations = forbiddenHandlerTokens
            .Where(token => exportHandlerSource.Contains(token, StringComparison.Ordinal))
            .Select(token => $"Details export handlers contain {token}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(sourceViolations);
    }

    [Fact]
    public void ProductionSource_DoesNotIntroduceOwnRagPipelineVectorDbAgenticWorkflowOrStage6SchemaChanges()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenExpansionTokens = new[]
        {
            "Embedding",
            "Embeddings",
            "IEmbedding",
            "Rerank",
            "IRerank",
            "RetrievalPipeline",
            "IRetrievalPipeline",
            "VectorDb",
            "VectorDatabase",
            "VectorStore",
            "Qdrant",
            "Pinecone",
            "Chroma",
            "Milvus",
            "AgenticWorkflow",
            "AgentOrchestrator",
            "IAgent"
        };

        var sourceViolations = EnumerateProductionSourceFiles(repositoryRoot)
            .SelectMany(file => FindForbiddenTokens(repositoryRoot, file, forbiddenExpansionTokens))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(sourceViolations);

        var forbiddenSchemaBoundaryTokens = new[]
        {
            "MigrationBuilder",
            "CreateTable(",
            "DropTable(",
            "AddColumn(",
            "AlterColumn(",
            "DropColumn(",
            "CreateIndex(",
            "DropIndex(",
            "AddForeignKey(",
            "DropForeignKey(",
            "RenameColumn(",
            "RenameTable(",
            "ModelBuilder",
            "EntityTypeBuilder",
            "IEntityTypeConfiguration",
            "HasColumnType(",
            "HasColumnName(",
            "HasIndex(",
            "HasKey(",
            "ToTable(",
            "OwnsOne(",
            "OwnsMany(",
            "UseSqlite(",
            "UseSqlServer(",
            "Database.Migrate",
            "EnsureCreated",
            "EnsureDeleted",
            "GetConnectionString",
            "ConnectionStrings",
            "UserSecrets",
            "ApiKey",
            "Authorization",
            "Bearer"
        };
        var schemaBoundaryViolations = EnumeratePageSourceFiles(repositoryRoot)
            .Concat(EnumerateExportSourceFiles(repositoryRoot))
            .SelectMany(file => FindForbiddenTokens(repositoryRoot, file, forbiddenSchemaBoundaryTokens))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(schemaBoundaryViolations);

        var forbiddenStage6MigrationTokens = new[]
        {
            "Stage6",
            "Stage 6",
            "ExportBoundary",
            "BoundaryExport",
            "ExportJson",
            "JsonExport",
            "ExportMarkdown",
            "MarkdownExport",
            "ExportDownload",
            "DownloadExport",
            "UiBoundary",
            "PageModel",
            "RazorPage"
        };
        var migrationSourceViolations = EnumerateMigrationSourceFiles(repositoryRoot)
            .SelectMany(file => FindForbiddenTokens(repositoryRoot, file, forbiddenStage6MigrationTokens));
        var migrationFileNameViolations = EnumerateMigrationSourceFiles(repositoryRoot)
            .SelectMany(file => forbiddenStage6MigrationTokens
                .Where(token => Path.GetFileName(file).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(repositoryRoot, file)} filename contains {token}"));
        var stage6MigrationViolations = migrationSourceViolations
            .Concat(migrationFileNameViolations)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(stage6MigrationViolations);
    }

    private static bool IsDifySpecificType(Type type) =>
        type.Namespace?.StartsWith(DifyNamespace, StringComparison.Ordinal) == true ||
        type.Name.Contains("Dify", StringComparison.Ordinal);

    private static bool IsExportType(Type type) =>
        type.Namespace == ExportNamespace ||
        type.Namespace?.StartsWith(ExportNamespace + ".", StringComparison.Ordinal) == true;

    private static bool IsPageType(Type type) =>
        type.Namespace == PagesNamespace ||
        type.Namespace?.StartsWith(PagesNamespace + ".", StringComparison.Ordinal) == true;

    private static bool IsExternalAdapterContractType(Type type) =>
        type.Namespace == "RequirementImpactAssistant.Web.Application.Analysis.External";

    private static bool IsProviderPayloadType(Type type) =>
        type == typeof(LlmProviderRequest) ||
        type == typeof(LlmProviderResponse) ||
        type == typeof(LlmProviderResponseStatus);

    private static string ReadProductionFile(string repositoryRoot, params string[] pathParts) =>
        File.ReadAllText(Path.Combine(
            [
                repositoryRoot,
                "src",
                WebProjectName,
                ..pathParts
            ]));

    private static string[] EnumeratePageSourceFiles(string repositoryRoot)
    {
        var pagesDirectory = Path.Combine(repositoryRoot, "src", WebProjectName, "Pages");

        return Directory
            .EnumerateFiles(pagesDirectory, "*.*", SearchOption.AllDirectories)
            .Where(IsProductionSourceFile)
            .Where(file => !IsUnderIgnoredDirectory(file))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] EnumerateProductionSourceFiles(string repositoryRoot)
    {
        var webRoot = Path.Combine(repositoryRoot, "src", WebProjectName);

        return Directory
            .EnumerateFiles(webRoot, "*.*", SearchOption.AllDirectories)
            .Where(IsProductionSourceFile)
            .Where(file => !IsUnderIgnoredDirectory(file))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] EnumerateExportSourceFiles(string repositoryRoot)
    {
        var exportDirectory = Path.Combine(repositoryRoot, "src", WebProjectName, "Application", "Export");

        return Directory
            .EnumerateFiles(exportDirectory, "*.*", SearchOption.AllDirectories)
            .Where(IsProductionSourceFile)
            .Where(file => !IsUnderIgnoredDirectory(file))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] EnumerateMigrationSourceFiles(string repositoryRoot)
    {
        var migrationsDirectory = Path.Combine(repositoryRoot, "src", WebProjectName, "Data", "Migrations");

        return Directory
            .EnumerateFiles(migrationsDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsProductionSourceFile(string file) =>
        file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
        file.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) ||
        file.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnderIgnoredDirectory(string file)
    {
        var parts = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return parts.Any(part =>
            part.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("wwwroot", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> FindForbiddenTokens(
        string repositoryRoot,
        string file,
        IEnumerable<string> forbiddenTokens)
    {
        var source = File.ReadAllText(file);

        return forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .Select(token => $"{Path.GetRelativePath(repositoryRoot, file)} contains {token}");
    }

    private static string GetSourceBetween(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, StringComparison.Ordinal);

        Assert.True(start >= 0, $"Could not find source marker: {startMarker}");
        Assert.True(end > start, $"Could not find source marker after start: {endMarker}");

        return source[start..end];
    }

    private static IEnumerable<Type> GetReferencedTypes(Type type)
    {
        var flags = BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        foreach (var referencedType in ExpandType(type.BaseType))
        {
            yield return referencedType;
        }

        foreach (var interfaceType in type.GetInterfaces())
        {
            foreach (var referencedType in ExpandType(interfaceType))
            {
                yield return referencedType;
            }
        }

        foreach (var constructor in type.GetConstructors(flags))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                foreach (var referencedType in ExpandType(parameter.ParameterType))
                {
                    yield return referencedType;
                }
            }
        }

        foreach (var field in type.GetFields(flags))
        {
            foreach (var referencedType in ExpandType(field.FieldType))
            {
                yield return referencedType;
            }
        }

        foreach (var property in type.GetProperties(flags))
        {
            foreach (var referencedType in ExpandType(property.PropertyType))
            {
                yield return referencedType;
            }
        }

        foreach (var method in type.GetMethods(flags))
        {
            foreach (var referencedType in ExpandType(method.ReturnType))
            {
                yield return referencedType;
            }

            foreach (var parameter in method.GetParameters())
            {
                foreach (var referencedType in ExpandType(parameter.ParameterType))
                {
                    yield return referencedType;
                }
            }
        }
    }

    private static IEnumerable<Type> ExpandType(Type? type)
    {
        if (type is null)
        {
            yield break;
        }

        if (type.IsArray || type.IsByRef || type.IsPointer)
        {
            foreach (var referencedType in ExpandType(type.GetElementType()))
            {
                yield return referencedType;
            }

            yield break;
        }

        yield return type;

        if (!type.IsGenericType)
        {
            yield break;
        }

        foreach (var genericArgument in type.GetGenericArguments())
        {
            foreach (var referencedType in ExpandType(genericArgument))
            {
                yield return referencedType;
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "RequirementImpactAssistant.sln");

            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate RequirementImpactAssistant.sln.");
    }
}
