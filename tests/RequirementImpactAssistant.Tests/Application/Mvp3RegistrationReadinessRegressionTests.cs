using System.Text.RegularExpressions;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class Mvp3RegistrationReadinessRegressionTests
{
    private const string WebProjectName = "RequirementImpactAssistant.Web";
    private const string TestsProjectName = "RequirementImpactAssistant.Tests";

    [Fact]
    public void PagesAndExportSources_DoNotDependDirectlyOnDifyAdapterOptionsOrNetworkClients()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guardedFiles = EnumerateSourceFiles(
            repositoryRoot,
            "src/RequirementImpactAssistant.Web/Pages",
            "src/RequirementImpactAssistant.Web/Application/Export");
        var forbiddenTokens = new[]
        {
            "DifyExternalRagAdapter",
            "DifyExternalRagOptions",
            "DifyExternalRagConfigurationStatus",
            "ExternalRag:Dify",
            "DifyWorkflow",
            "DifyAgent",
            "IExternalRagAdapter",
            "ILlmProvider",
            "HttpClient",
            "HttpMessageHandler",
            "HttpRequestMessage",
            "HttpResponseMessage",
            "IHttp" + "ClientFactory"
        };

        var violations = guardedFiles
            .SelectMany(file => FindForbiddenTokens(repositoryRoot, file, forbiddenTokens))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void UiDomainAndExportSources_DoNotExposeProviderSpecificDtos()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guardedFiles = EnumerateSourceFiles(
            repositoryRoot,
            "src/RequirementImpactAssistant.Web/Pages",
            "src/RequirementImpactAssistant.Web/Domain",
            "src/RequirementImpactAssistant.Web/Application/Export");
        var providerDtoPatterns = new[]
        {
            @"\bDify\w*Dto\b",
            @"\bDifyWorkflow\w+\b",
            @"\bDifyAgent\w+\b",
            @"\bLlmProvider(Request|Response|ResponseStatus)\b",
            @"\bExternalRagAdapter(Request|Response|ResponseMetadata|ResponseStatus)\b"
        };

        var violations = guardedFiles
            .SelectMany(file => FindForbiddenPatterns(repositoryRoot, file, providerDtoPatterns))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ExportSources_DoNotCallAnalysisEnginesProviderAdaptersOrNetworkApis()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guardedFiles = EnumerateSourceFiles(
            repositoryRoot,
            "src/RequirementImpactAssistant.Web/Application/Export");
        var forbiddenTokens = new[]
        {
            "IAiAnalysisEngine",
            "IAnalysisExecutionService",
            "AnalysisExecutionService",
            "IAiAnalysisEngineSelector",
            "IExternalRagAdapter",
            "ExternalRagAdapter",
            "ILlmProvider",
            "DirectLlmAnalysisEngine",
            "ExternalRagAnalysisEngine",
            "DeepSeek",
            "Dify",
            ".AnalyzeAsync(",
            ".CompleteAsync(",
            ".RunAsync(",
            ".SendAsync(",
            ".PostAsync(",
            ".GetAsync(",
            ".GetStringAsync(",
            "HttpClient",
            "HttpMessageHandler",
            "HttpRequestMessage",
            "HttpResponseMessage"
        };

        var violations = guardedFiles
            .SelectMany(file => FindForbiddenTokens(repositoryRoot, file, forbiddenTokens))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void DefaultTestInfrastructure_RemainsOfflineAndDoesNotAddLiveProviderGates()
    {
        var repositoryRoot = FindRepositoryRoot();
        var testProjectFile = Path.Combine(
            repositoryRoot,
            "tests",
            TestsProjectName,
            TestsProjectName + ".csproj");
        var testProjectSource = File.ReadAllText(testProjectFile);
        var forbiddenPackageTokens = new[]
        {
            "Microsoft.AspNetCore.Mvc." + "Testing",
            "Microsoft.AspNetCore." + "TestHost",
            "Microsoft." + "Playwright",
            "OpenQA." + "Selenium",
            "WireMock",
            "Testcontainers"
        };
        var forbiddenSourceTokens = new[]
        {
            "RUN_DIFY",
            "REAL_PROVIDER",
            "DIFY_" + "ENDPOINT",
            "DIFY_" + "API_" + "KEY",
            "DEEPSEEK_" + "API_" + "KEY",
            ".AddUserSecrets",
            "SecretManager",
            "Environment.GetEnvironmentVariable"
        };
        var sourceFiles = EnumerateSourceFiles(
                repositoryRoot,
                "tests/RequirementImpactAssistant.Tests")
            .Where(file => !IsStaticRegressionGuard(file))
            .Where(file => !IsOptionalProviderContractTest(file));

        var packageViolations = forbiddenPackageTokens
            .Where(token => testProjectSource.Contains(token, StringComparison.Ordinal))
            .Select(token => $"{NormalizeRelativePath(repositoryRoot, testProjectFile)} contains package/infrastructure token {token}");
        var sourceViolations = sourceFiles
            .SelectMany(file => FindForbiddenTokens(repositoryRoot, file, forbiddenSourceTokens));
        var violations = packageViolations
            .Concat(sourceViolations)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static IEnumerable<string> EnumerateSourceFiles(string repositoryRoot, params string[] relativeRoots)
    {
        foreach (var relativeRoot in relativeRoots)
        {
            var root = Path.Combine([repositoryRoot, ..relativeRoot.Split('/')]);

            foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                if (IsUnderIgnoredDirectory(file) ||
                    !IsSourceFile(file))
                {
                    continue;
                }

                yield return file;
            }
        }
    }

    private static IEnumerable<string> FindForbiddenTokens(
        string repositoryRoot,
        string file,
        IEnumerable<string> forbiddenTokens)
    {
        var source = File.ReadAllText(file);
        var relativePath = NormalizeRelativePath(repositoryRoot, file);

        foreach (var forbiddenToken in forbiddenTokens)
        {
            if (source.Contains(forbiddenToken, StringComparison.Ordinal))
            {
                yield return $"{relativePath} contains {forbiddenToken}";
            }
        }
    }

    private static IEnumerable<string> FindForbiddenPatterns(
        string repositoryRoot,
        string file,
        IEnumerable<string> forbiddenPatterns)
    {
        var source = File.ReadAllText(file);
        var relativePath = NormalizeRelativePath(repositoryRoot, file);

        foreach (var forbiddenPattern in forbiddenPatterns)
        {
            if (Regex.IsMatch(source, forbiddenPattern))
            {
                yield return $"{relativePath} matches {forbiddenPattern}";
            }
        }
    }

    private static bool IsSourceFile(string file)
    {
        var extension = Path.GetExtension(file);

        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStaticRegressionGuard(string file)
    {
        var fileName = Path.GetFileName(file);

        return fileName.Contains("ArchitectureRegressionTests", StringComparison.Ordinal) ||
            fileName.Contains("ReproducibilityRegressionTests", StringComparison.Ordinal) ||
            fileName.Equals(nameof(Mvp3RegistrationReadinessRegressionTests) + ".cs", StringComparison.Ordinal);
    }

    private static bool IsOptionalProviderContractTest(string file)
    {
        var fileName = Path.GetFileName(file);

        return fileName.Equals("DeepSeekLlmProviderTests.cs", StringComparison.Ordinal) ||
            fileName.Equals("DifyExternalRagAdapterTests.cs", StringComparison.Ordinal) ||
            fileName.Equals("DifyAgentRequestContractTests.cs", StringComparison.Ordinal) ||
            fileName.Equals("DifyAgentSseStreamParserTests.cs", StringComparison.Ordinal) ||
            fileName.Equals("DifyAgentAnswerJsonParserTests.cs", StringComparison.Ordinal);
    }

    private static bool IsUnderIgnoredDirectory(string file)
    {
        var parts = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return parts.Any(part =>
            part.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            part.Equals(".git", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeRelativePath(string repositoryRoot, string file) =>
        Path.GetRelativePath(repositoryRoot, file).Replace(Path.DirectorySeparatorChar, '/');

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
