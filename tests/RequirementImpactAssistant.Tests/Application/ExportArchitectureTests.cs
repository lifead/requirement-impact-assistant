using System.Reflection;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.Llm;
using RequirementImpactAssistant.Web.Application.Export;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class ExportArchitectureTests
{
    private static readonly Type[] ExportTypes = typeof(AnalysisMarkdownExportService).Assembly
        .GetTypes()
        .Where(type => type.Namespace == "RequirementImpactAssistant.Web.Application.Export")
        .ToArray();

    [Fact]
    public void ExportTypes_DoNotDependOnAnalysisEnginesLlmProvidersOrNetworkClients()
    {
        var forbiddenTypes = new[]
        {
            typeof(IAiAnalysisEngine),
            typeof(ILlmProvider),
            typeof(DirectLlmAnalysisEngine),
            typeof(DemoLlmProvider),
            typeof(DeepSeekLlmProvider),
            typeof(HttpClient),
            typeof(HttpMessageHandler),
            typeof(HttpRequestMessage),
            typeof(HttpResponseMessage)
        };

        var violations = ExportTypes
            .SelectMany(type => GetReferencedTypes(type)
                .Where(referencedType => forbiddenTypes.Any(forbiddenType => forbiddenType.IsAssignableFrom(referencedType)))
                .Select(referencedType => $"{type.FullName} -> {referencedType.FullName}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ExportSourceFiles_DoNotReferenceProviderAdapterNetworkOrRetrievalImplementations()
    {
        var exportDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "RequirementImpactAssistant.Web",
            "Application",
            "Export");

        var forbiddenTokens = new[]
        {
            "IAiAnalysisEngine",
            "IAnalysisExecutionService",
            "AnalysisExecutionService",
            "ILlmProvider",
            "DirectLlmAnalysisEngine",
            "DemoLlmProvider",
            "DeepSeek",
            "Dify",
            "HttpClient",
            "HttpMessageHandler",
            "HttpRequestMessage",
            "HttpResponseMessage",
            "Embedding",
            "Embeddings",
            "Rerank",
            "VectorDb",
            "VectorDatabase",
            "Qdrant",
            "Pinecone"
        };

        var violations = Directory.EnumerateFiles(exportDirectory, "*.cs")
            .SelectMany(file => forbiddenTokens
                .Where(token => File.ReadAllText(file).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetFileName(file)} contains {token}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
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
