using System.Reflection;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.External;
using RequirementImpactAssistant.Web.Application.Analysis.External.Dify;
using RequirementImpactAssistant.Web.Application.Export;
using RequirementImpactAssistant.Web.Domain;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class Stage5ArchitectureRegressionTests
{
    private const string DifyNamespace = "RequirementImpactAssistant.Web.Application.Analysis.External.Dify";
    private const string ExportNamespace = "RequirementImpactAssistant.Web.Application.Export";

    private static readonly Assembly WebAssembly = typeof(IAiAnalysisEngine).Assembly;

    [Fact]
    public void ProductionDifyTypes_LiveOnlyInsideDifyAdapterArea()
    {
        var violations = WebAssembly
            .GetTypes()
            .Where(IsDifySpecificType)
            .Where(type => type.Namespace is null ||
                !type.Namespace.StartsWith(DifyNamespace, StringComparison.Ordinal))
            .Select(type => type.FullName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void DomainPagesExportAndDirectLlm_DoNotReferenceDifySpecificTypes()
    {
        var guardedTypes = WebAssembly
            .GetTypes()
            .Where(type =>
                IsDomainType(type) ||
                type.IsAssignableTo(typeof(PageModel)) ||
                IsExportType(type) ||
                type == typeof(DirectLlmAnalysisEngine))
            .ToArray();

        var violations = guardedTypes
            .SelectMany(type => GetReferencedTypes(type)
                .Where(IsDifySpecificType)
                .Select(referencedType => $"{type.FullName} -> {referencedType.FullName}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void GuardedProductionSource_DoesNotContainDifyTokens()
    {
        var repositoryRoot = FindRepositoryRoot();
        var violations = EnumerateDifyLeakageGuardedProductionSourceFiles(repositoryRoot)
            .SelectMany(file =>
            {
                var source = File.ReadAllText(file);

                return new[] { "Dify", "dify", "DIFY" }
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, file)} contains {token}");
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void DirectLlmSource_DoesNotReferenceDifyOrExternalRagAdapterTokens()
    {
        var repositoryRoot = FindRepositoryRoot();
        var file = Path.Combine(
            repositoryRoot,
            "src",
            "RequirementImpactAssistant.Web",
            "Application",
            "Analysis",
            "DirectLlmAnalysisEngine.cs");

        var source = File.ReadAllText(file);
        var forbiddenTokens = new[]
        {
            "RequirementImpactAssistant.Web.Application.Analysis.External",
            "IExternalRagAdapter",
            "ExternalRagAdapter",
            "MockExternalRagAdapter",
            "Dify",
            "dify"
        };

        var violations = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .Select(token => $"{Path.GetRelativePath(repositoryRoot, file)} contains {token}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void StableJsonExportSource_DoesNotReferenceDifySpecificContracts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var exportFiles = new[]
        {
            Path.Combine(
                repositoryRoot,
                "src",
                "RequirementImpactAssistant.Web",
                "Application",
                "Export",
                "AnalysisJsonExportService.cs"),
            Path.Combine(
                repositoryRoot,
                "src",
                "RequirementImpactAssistant.Web",
                "Application",
                "Export",
                "AnalysisJsonReportBuilder.cs")
        };
        var forbiddenTokens = new[]
        {
            "Dify",
            "dify",
            "DifyExternal",
            "DifyWorkflow",
            "workflow_run_id",
            "workflow_id",
            "task_id",
            "response_mode"
        };

        var violations = exportFiles
            .SelectMany(file =>
            {
                var source = File.ReadAllText(file);

                return forbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, file)} contains {token}");
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void CommittedProductionConfiguration_DoesNotContainDifyEndpointApiKeyOrSecrets()
    {
        var repositoryRoot = FindRepositoryRoot();
        var configFiles = EnumerateProductionConfigurationFiles(repositoryRoot);
        var forbiddenTokens = new[]
        {
            "ExternalRag",
            "Dify",
            "dify",
            "ApiKey",
            "Authorization",
            "Bearer",
            "Secret",
            "UserSecrets"
        };

        var violations = configFiles
            .SelectMany(file =>
            {
                var source = File.ReadAllText(file);

                return forbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, file)} contains {token}");
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionSource_DoesNotHardcodeDifyEndpointOrApiKeyValues()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenTokens = new[]
        {
            "dify.invalid",
            "test-dify-api-key",
            "sk-",
            "api-key",
            "x-api-key"
        };

        var violations = EnumerateProductionSourceFiles(repositoryRoot)
            .SelectMany(file =>
            {
                var source = File.ReadAllText(file);

                return forbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.OrdinalIgnoreCase))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, file)} contains {token}");
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionSource_DoesNotIntroduceOwnRagRetrievalPipelineEmbeddingsRerankVectorDbOrAgenticWorkflow()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenTokens = new[]
        {
            "Embedding",
            "Embeddings",
            "IEmbedding",
            "Rerank",
            "IRerank",
            "RetrievalPipeline",
            "VectorDb",
            "VectorDatabase",
            "VectorStore",
            "Qdrant",
            "Pinecone",
            "Chroma",
            "Milvus",
            "AgenticWorkflow"
        };

        var violations = EnumerateProductionSourceFiles(repositoryRoot)
            .SelectMany(file =>
            {
                var source = File.ReadAllText(file);

                return forbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, file)} contains {token}");
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static bool IsDifySpecificType(Type type) =>
        type.Namespace?.StartsWith(DifyNamespace, StringComparison.Ordinal) == true ||
        type.Name.Contains("Dify", StringComparison.Ordinal);

    private static bool IsDomainType(Type type) =>
        type.Namespace == typeof(AiAnalysisResult).Namespace ||
        type.Namespace?.StartsWith(typeof(AiAnalysisResult).Namespace + ".", StringComparison.Ordinal) == true;

    private static bool IsExportType(Type type) =>
        type.Namespace == ExportNamespace ||
        type.Namespace?.StartsWith(ExportNamespace + ".", StringComparison.Ordinal) == true;

    private static string[] EnumerateProductionConfigurationFiles(string repositoryRoot)
    {
        var webRoot = Path.Combine(repositoryRoot, "src", "RequirementImpactAssistant.Web");

        return Directory
            .EnumerateFiles(webRoot, "*.json", SearchOption.AllDirectories)
            .Where(file => !IsUnderIgnoredDirectory(file))
            .Where(file =>
                Path.GetFileName(file).StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(file).Equals("launchSettings.json", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] EnumerateDifyLeakageGuardedProductionSourceFiles(string repositoryRoot)
    {
        var webRoot = Path.Combine(repositoryRoot, "src", "RequirementImpactAssistant.Web");
        var guardedRoots = new[]
        {
            Path.Combine(webRoot, "Domain"),
            Path.Combine(webRoot, "Pages"),
            Path.Combine(webRoot, "Application", "Export")
        };
        var guardedFiles = new[]
        {
            Path.Combine(webRoot, "Application", "Analysis", "DirectLlmAnalysisEngine.cs")
        };

        return guardedRoots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            .Concat(guardedFiles.Where(File.Exists))
            .Where(file => !IsUnderIgnoredDirectory(file))
            .Where(IsProductionSourceFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] EnumerateProductionSourceFiles(string repositoryRoot)
    {
        var webRoot = Path.Combine(repositoryRoot, "src", "RequirementImpactAssistant.Web");

        return Directory
            .EnumerateFiles(webRoot, "*.*", SearchOption.AllDirectories)
            .Where(file => !IsUnderIgnoredDirectory(file))
            .Where(IsProductionSourceFile)
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
