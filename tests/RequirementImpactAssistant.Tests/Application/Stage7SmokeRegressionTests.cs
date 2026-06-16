namespace RequirementImpactAssistant.Tests.Application;

public sealed class Stage7SmokeRegressionTests
{
    private const string WebProjectName = "RequirementImpactAssistant.Web";
    private const string TestsProjectName = "RequirementImpactAssistant.Tests";
    private const string ThisFileName = nameof(Stage7SmokeRegressionTests) + ".cs";

    [Fact]
    public void ProductionSource_DoesNotContainStage7SmokeAssetsOrTokens()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenTokens = new[]
        {
            "Mvp1Smoke",
            "local-smoke",
            "LocalSmokeFixtureAdapter",
            "mvp1-smoke-baseline"
        };

        var violations = EnumerateProductionFiles(repositoryRoot)
            .SelectMany(file => FindForbiddenTokens(repositoryRoot, file, forbiddenTokens))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void SmokeTestAndHelperFiles_DoNotUseBrowserOrHostedWebTestFrameworks()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenTokens = new[]
        {
            "Microsoft.Playwright",
            "Playwright",
            "IPlaywright",
            "IBrowser",
            "IBrowserContext",
            "IPage",
            "PageTest",
            "BrowserTest",
            "Microsoft.Playwright.Xunit",
            "OpenQA.Selenium",
            "IWebDriver",
            "WebDriver",
            "WebApplicationFactory",
            "TestServer",
            "Microsoft.AspNetCore.Mvc.Testing",
            "Microsoft.AspNetCore.TestHost"
        };

        var violations = EnumerateSmokeTestAndHelperFiles(repositoryRoot)
            .Where(file => !Path.GetFileName(file).Equals(ThisFileName, StringComparison.Ordinal))
            .SelectMany(file => FindForbiddenTokens(repositoryRoot, file, forbiddenTokens))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Stage7SmokePath_DoesNotRequireRealNetworkProvidersOrAdapters()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenTokens = new[]
        {
            "HttpClient",
            "HttpMessageHandler",
            "HttpRequestMessage",
            "HttpResponseMessage",
            "IHttpClientFactory",
            ".SendAsync(",
            ".GetStringAsync(",
            ".PostAsync(",
            "DeepSeekLlmProvider",
            "DifyExternalRagAdapter"
        };

        var violations = EnumerateSmokeSections(repositoryRoot)
            .SelectMany(section => forbiddenTokens
                .Where(token => section.Source.Contains(token, StringComparison.Ordinal))
                .Select(token => $"{section.RelativePath} {section.Name} contains {token}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionConfigAndMigrations_DoNotContainStage7SmokeTokensOrSchemaConfigSmokeChanges()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenTokens = new[]
        {
            "Mvp1Smoke",
            "local-smoke",
            "LocalSmoke",
            "LocalSmokeFixtureAdapter",
            "LocalSmokeDirectProvider",
            "LocalMockKnowledgeSource",
            "mvp1-smoke-baseline",
            "local demo request - example integration boundary",
            "example integration boundary",
            "demo requirement catalogue"
        };

        var violations = EnumerateProductionConfigAndMigrationFiles(repositoryRoot)
            .SelectMany(file => FindForbiddenTokens(repositoryRoot, file, forbiddenTokens))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Stage7SmokeChecklist_CoversTasks1Through4ByExpectedFilesAndTestNames()
    {
        var repositoryRoot = FindRepositoryRoot();
        var expectedFileMarkers = new Dictionary<string, string[]>
        {
            ["tests/RequirementImpactAssistant.Tests/Support/Mvp1SmokeBaselineFixture.cs"] =
            [
                "public static class Mvp1SmokeBaselineFixture",
                "CreateSavedDirectLlmAnalysis",
                "CreateSavedExternalRagAnalysis",
                "ExternalHappyPathResponse"
            ],
            ["tests/RequirementImpactAssistant.Tests/Support/Mvp1SmokeBaselineFixtureTests.cs"] =
            [
                "Create_ReturnsDeterministicStableBaseline",
                "Create_ExternalBaselineHasRetrievedContextAvailableHappyPath",
                "Create_UsesOnlyLocalFakesAndNoRealNetworkReferences"
            ],
            ["tests/RequirementImpactAssistant.Tests/Application/AnalysisExecutionServiceTests.cs"] =
            [
                "RunAsync_Mvp1SmokeBaselineDirectLlmPathPersistsImpactMapAndNeutralMetadata",
                "RunAsync_Mvp1SmokeBaselineExternalRagPathPersistsImpactMapMetadataAndRetrievedContext"
            ],
            ["tests/RequirementImpactAssistant.Tests/Application/AnalysisMarkdownExportServiceTests.cs"] =
            [
                "ExportAsync_ExportsSavedMvp1DirectLlmSmokeBaselineHumanLayerAndImpactMap",
                "ExportAsync_ExportsSavedMvp1ExternalRagSmokeBaselineHumanLayerRetrievedContextAndImpactMap"
            ],
            ["tests/RequirementImpactAssistant.Tests/Application/AnalysisJsonExportServiceTests.cs"] =
            [
                "ExportAsync_ExportsSavedMvp1DirectLlmSmokeBaselineHumanLayerAndImpactMap",
                "ExportAsync_ExportsSavedMvp1ExternalRagSmokeBaselineHumanLayerRetrievedContextAndImpactMap"
            ]
        };

        var violations = expectedFileMarkers
            .SelectMany(entry => FindMissingMarkers(repositoryRoot, entry.Key, entry.Value))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static IEnumerable<string> FindMissingMarkers(
        string repositoryRoot,
        string relativePath,
        IEnumerable<string> expectedMarkers)
    {
        var file = Path.Combine(
            [
                repositoryRoot,
                ..relativePath.Split('/')
            ]);

        if (!File.Exists(file))
        {
            yield return $"{relativePath} is missing";
            yield break;
        }

        var source = File.ReadAllText(file);

        foreach (var expectedMarker in expectedMarkers)
        {
            if (!source.Contains(expectedMarker, StringComparison.Ordinal))
            {
                yield return $"{relativePath} is missing {expectedMarker}";
            }
        }
    }

    private static IEnumerable<SmokeSection> EnumerateSmokeSections(string repositoryRoot)
    {
        var smokeMethodNames = new[]
        {
            "RunAsync_Mvp1SmokeBaselineDirectLlmPathPersistsImpactMapAndNeutralMetadata",
            "RunAsync_Mvp1SmokeBaselineExternalRagPathPersistsImpactMapMetadataAndRetrievedContext",
            "ExportAsync_ExportsSavedMvp1DirectLlmSmokeBaselineHumanLayerAndImpactMap",
            "ExportAsync_ExportsSavedMvp1ExternalRagSmokeBaselineHumanLayerRetrievedContextAndImpactMap"
        };

        foreach (var file in EnumerateSmokeTestAndHelperFiles(repositoryRoot)
            .Where(file => !Path.GetFileName(file).Equals(ThisFileName, StringComparison.Ordinal)))
        {
            var relativePath = NormalizeRelativePath(repositoryRoot, file);
            var source = File.ReadAllText(file);

            if (relativePath.StartsWith("tests/RequirementImpactAssistant.Tests/Support/", StringComparison.Ordinal))
            {
                yield return new SmokeSection(relativePath, Path.GetFileName(file), source);
                continue;
            }

            foreach (var methodName in smokeMethodNames)
            {
                var section = TryExtractMethod(relativePath, source, methodName);

                if (section is not null)
                {
                    yield return section;
                }
            }
        }
    }

    private static SmokeSection? TryExtractMethod(string relativePath, string source, string methodName)
    {
        var nameIndex = source.IndexOf(methodName, StringComparison.Ordinal);
        if (nameIndex < 0)
        {
            return null;
        }

        var methodStart = source.LastIndexOf("public async Task ", nameIndex, StringComparison.Ordinal);
        if (methodStart < 0)
        {
            methodStart = source.LastIndexOf("public void ", nameIndex, StringComparison.Ordinal);
        }

        Assert.True(methodStart >= 0, $"Could not find method start for {relativePath} {methodName}.");

        var bodyStart = source.IndexOf('{', methodStart);
        Assert.True(bodyStart >= 0, $"Could not find method body start for {relativePath} {methodName}.");

        var bodyEnd = FindMatchingBrace(source, bodyStart);
        Assert.True(bodyEnd >= 0, $"Could not find method body end for {relativePath} {methodName}.");

        return new SmokeSection(relativePath, methodName, source[methodStart..(bodyEnd + 1)]);
    }

    private static int FindMatchingBrace(string source, int openBraceIndex)
    {
        var depth = 0;

        for (var index = openBraceIndex; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static IEnumerable<string> FindForbiddenTokens(
        string repositoryRoot,
        string file,
        IEnumerable<string> forbiddenTokens)
    {
        var source = File.ReadAllText(file);
        var relativePath = NormalizeRelativePath(repositoryRoot, file);

        return forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .Select(token => $"{relativePath} contains {token}");
    }

    private static string[] EnumerateSmokeTestAndHelperFiles(string repositoryRoot)
    {
        var testRoot = Path.Combine(repositoryRoot, "tests", TestsProjectName);

        return Directory
            .EnumerateFiles(testRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsUnderIgnoredDirectory(file))
            .Where(file =>
            {
                var source = File.ReadAllText(file);

                return source.Contains("Mvp1Smoke", StringComparison.Ordinal) ||
                    source.Contains("local-smoke", StringComparison.Ordinal) ||
                    source.Contains("LocalSmokeFixtureAdapter", StringComparison.Ordinal) ||
                    source.Contains("mvp1-smoke-baseline", StringComparison.Ordinal);
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] EnumerateProductionFiles(string repositoryRoot)
    {
        var productionRoot = Path.Combine(repositoryRoot, "src", WebProjectName);

        return Directory
            .EnumerateFiles(productionRoot, "*.*", SearchOption.AllDirectories)
            .Where(IsSourceOrConfigFile)
            .Where(file => !IsUnderIgnoredDirectory(file))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] EnumerateProductionConfigAndMigrationFiles(string repositoryRoot)
    {
        var productionRoot = Path.Combine(repositoryRoot, "src", WebProjectName);
        var migrationsRoot = Path.Combine(productionRoot, "Data", "Migrations");

        return Directory
            .EnumerateFiles(productionRoot, "*.*", SearchOption.AllDirectories)
            .Where(file =>
                file.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                file.StartsWith(migrationsRoot, StringComparison.OrdinalIgnoreCase))
            .Where(file => !IsUnderIgnoredDirectory(file))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsSourceOrConfigFile(string file) =>
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

    private static string NormalizeRelativePath(string repositoryRoot, string file) =>
        Path.GetRelativePath(repositoryRoot, file)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

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

    private sealed record SmokeSection(string RelativePath, string Name, string Source);
}
