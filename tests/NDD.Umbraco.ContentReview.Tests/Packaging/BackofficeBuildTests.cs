using NDD.Umbraco.ContentReview.Tests.Infrastructure;

namespace NDD.Umbraco.ContentReview.Tests.Packaging;

/// <summary>
/// The Backoffice project's client-build targets, run against the clean copy that
/// <see cref="PackedPackages"/> has already packed, so these builds stay away from this run's binaries.
/// </summary>
public sealed class BackofficeBuildTests(PackedPackages packed)
{
    private string BackofficeProject => Path.Combine(
        packed.CopyDirectory, "src", "NDD.Umbraco.ContentReview.Backoffice", "NDD.Umbraco.ContentReview.Backoffice.csproj");

    [Fact]
    public void Manifest_stamp_fails_the_build_when_the_placeholder_is_missing()
    {
        string work = Directory.CreateTempSubdirectory("ndd-content-review-stamp-").FullName;
        try
        {
            string client = Path.Combine(work, "Client");
            string output = Path.Combine(work, "out");
            Directory.CreateDirectory(Path.Combine(client, "public"));
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(client, "public", "umbraco-package.json"), """{ "name": "Content Review", "version": "1.2.3" }""");

            ProcessResult result = ProcessRunner.Run(
                ProcessRunner.DotNet,
                [
                    "msbuild", BackofficeProject, "-t:ContentReviewStampManifestVersion", "-nologo",
                    "-p:ContentReviewBuildClient=false",
                    $"-p:ContentReviewClientDir={client}",
                    $"-p:ContentReviewClientOutputDir={output}",
                ],
                packed.CopyDirectory);

            Assert.False(result.Succeeded, "The build succeeded although the manifest had no version placeholder.\n" + result.Output);
            Assert.Contains("umbraco-package.json version was not stamped", result.Output);
            Assert.False(File.Exists(Path.Combine(output, "umbraco-package.json")), "An unstamped manifest was written.");
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    [Fact]
    public void Manifest_stamp_writes_the_package_version()
    {
        string work = Directory.CreateTempSubdirectory("ndd-content-review-stamp-").FullName;
        try
        {
            string client = Path.Combine(work, "Client");
            string output = Path.Combine(work, "out");
            Directory.CreateDirectory(Path.Combine(client, "public"));
            // An existing output folder is what makes the stamp run when the client build is off
            Directory.CreateDirectory(output);
            // ';' and '%3B' are the characters an MSBuild item list would split on or decode
            const string manifest = """{ "name": "Content Review; 100%3B", "version": "0.0.0" }""";
            File.WriteAllText(Path.Combine(client, "public", "umbraco-package.json"), manifest);

            ProcessResult result = ProcessRunner.Run(
                ProcessRunner.DotNet,
                [
                    "msbuild", BackofficeProject, "-t:ContentReviewStampManifestVersion", "-nologo",
                    "-p:ContentReviewBuildClient=false",
                    "-p:Version=17.9.9-test",
                    $"-p:ContentReviewClientDir={client}",
                    $"-p:ContentReviewClientOutputDir={output}",
                ],
                packed.CopyDirectory);

            Assert.True(result.Succeeded, result.Output);
            Assert.Equal(
                manifest.Replace("\"version\": \"0.0.0\"", "\"version\": \"17.9.9-test\""),
                File.ReadAllText(Path.Combine(output, "umbraco-package.json")).TrimEnd('\r', '\n'));
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    [Fact]
    public void Solution_builds_without_node_when_the_client_build_is_disabled()
    {
        // A PATH containing only the dotnet host: npm cannot be found, so any attempt to run it fails
        string dotnetDirectory = Path.GetDirectoryName(Path.GetFullPath(
            ProcessRunner.DotNet == "dotnet" ? FindOnPath("dotnet") : ProcessRunner.DotNet))!;
        string artifacts = Directory.CreateTempSubdirectory("ndd-content-review-nonode-").FullName;

        // Start from no built client, as on a fresh clone; pack built one into the copy
        string builtClient = Path.Combine(packed.CopyDirectory, "src", "NDD.Umbraco.ContentReview.Backoffice", "wwwroot", "App_Plugins");
        if (Directory.Exists(builtClient))
        {
            Directory.Delete(builtClient, recursive: true);
        }

        try
        {
            ProcessResult result = ProcessRunner.Run(
                ProcessRunner.DotNet,
                ["build", Repository.SolutionFileName, "-p:ContentReviewBuildClient=false", "--artifacts-path", artifacts, "-nologo"],
                packed.CopyDirectory,
                new Dictionary<string, string?> { ["PATH"] = dotnetDirectory });

            Assert.True(result.Succeeded, result.Output);
            Assert.False(Directory.Exists(builtClient), "A client was built although the client build was disabled.");
        }
        finally
        {
            Directory.Delete(artifacts, recursive: true);
        }
    }

    private static string FindOnPath(string name)
    {
        string[] extensions = OperatingSystem.IsWindows() ? [".exe", ""] : [""];
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            foreach (string extension in extensions)
            {
                string candidate = Path.Combine(directory, name + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        throw new InvalidOperationException($"{name} is not on the PATH.");
    }
}
