using System.IO.Compression;
using System.Xml.Linq;

namespace NDD.Umbraco.ContentReview.Tests.Infrastructure;

internal sealed record PackageDependency(string Id, string Range, string? TargetFramework);

internal sealed record PackedPackage(
    string Id,
    string Version,
    string Description,
    string Authors,
    string? LicenseExpression,
    string? Readme,
    string? Icon,
    IReadOnlyList<PackageDependency> Dependencies,
    IReadOnlyList<string> DependencyGroupFrameworks,
    IReadOnlyList<string> Files,
    string Path);

/// <summary>
/// Packs the solution ONCE per test run, from a copy of the repository with no build output in
/// it: no bin/obj, no node_modules, no built backoffice client. That is the state of a clean
/// clone, which is the state that matters: a client that only reaches the package because a
/// working copy happened to have it built already is the defect these tests exist to catch.
/// Packing a copy also keeps the nested build away from this test run's own locked binaries.
/// </summary>
public sealed class PackedPackages : IDisposable
{
    private static readonly string[] ExcludedDirectoryNames = ["bin", "obj", "node_modules", ".git", ".vs", ".serena", "artifacts"];

    private readonly string _workDirectory;

    public PackedPackages()
    {
        _workDirectory = Directory.CreateTempSubdirectory("ndd-content-review-pack-").FullName;
        CopyDirectory = Path.Combine(_workDirectory, "repo");
        OutputDirectory = Path.Combine(_workDirectory, "packages");

        CopyRepository(Repository.Root, CopyDirectory);

        ProcessResult pack = ProcessRunner.Run(
            ProcessRunner.DotNet,
            ["pack", Repository.SolutionFileName, "--configuration", "Release", "--output", OutputDirectory],
            CopyDirectory);

        if (!pack.Succeeded)
        {
            throw new InvalidOperationException(
                "dotnet pack of a clean copy of the repository failed. Packing builds the backoffice " +
                "client, so this needs Node (npm) on the PATH.\n" + pack.Output);
        }

        Packages = Directory.GetFiles(OutputDirectory, "*.nupkg")
            .Select(Read)
            .ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The clean copy of the repository that was packed. Shared by every test, and tests run in
    /// parallel across classes. Build into your own folder (--artifacts-path) or a fresh probe
    /// directory. BackofficeBuildTests deletes the copy's built client to simulate a fresh clone,
    /// so no test may rely on that client still being there.
    /// </summary>
    public string CopyDirectory { get; }

    public string OutputDirectory { get; }

    internal IReadOnlyDictionary<string, PackedPackage> Packages { get; }

    internal PackedPackage Get(string id) =>
        Packages.TryGetValue(id, out PackedPackage? package)
            ? package
            : throw new InvalidOperationException(
                $"No package {id} was produced. Produced: {string.Join(", ", Packages.Keys)}");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_workDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A build server can hold a file briefly; the OS cleans temp eventually.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void CopyRepository(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (string directory in Directory.GetDirectories(source))
        {
            string name = Path.GetFileName(directory);
            if (ExcludedDirectoryNames.Contains(name, StringComparer.OrdinalIgnoreCase) || IsBuildOutput(directory))
            {
                continue;
            }

            CopyRepository(directory, Path.Combine(destination, name));
        }
    }

    // The built backoffice client, and the TestSite's runtime data
    private static bool IsBuildOutput(string directory)
    {
        string name = Path.GetFileName(directory);
        string parent = Path.GetFileName(Path.GetDirectoryName(directory)) ?? string.Empty;

        return (name.Equals("App_Plugins", StringComparison.OrdinalIgnoreCase) && parent.Equals("wwwroot", StringComparison.OrdinalIgnoreCase))
            || (parent.Equals("umbraco", StringComparison.OrdinalIgnoreCase)
                && (name.Equals("Data", StringComparison.OrdinalIgnoreCase) || name.Equals("Logs", StringComparison.OrdinalIgnoreCase)));
    }

    private static PackedPackage Read(string path)
    {
        using ZipArchive zip = ZipFile.OpenRead(path);

        ZipArchiveEntry nuspecEntry = zip.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) && !e.FullName.Contains('/'));
        XDocument nuspec;
        using (Stream stream = nuspecEntry.Open())
        {
            nuspec = XDocument.Load(stream);
        }

        XElement metadata = nuspec.Root!.Elements().Single(e => e.Name.LocalName == "metadata");
        string? Value(string name) => metadata.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

        XElement? license = metadata.Elements().FirstOrDefault(e => e.Name.LocalName == "license");

        List<PackageDependency> dependencies = metadata
            .Descendants()
            .Where(e => e.Name.LocalName == "dependency")
            .Select(e => new PackageDependency(
                (string)e.Attribute("id")!,
                (string?)e.Attribute("version") ?? string.Empty,
                e.Parent is { Name.LocalName: "group" } group ? (string?)group.Attribute("targetFramework") : null))
            .ToList();

        List<string> groupFrameworks = metadata
            .Descendants()
            .Where(e => e.Name.LocalName == "group" && e.Parent?.Name.LocalName == "dependencies")
            .Select(e => (string?)e.Attribute("targetFramework") ?? string.Empty)
            .ToList();

        return new PackedPackage(
            Id: Value("id")!,
            Version: Value("version")!,
            Description: Value("description") ?? string.Empty,
            Authors: Value("authors") ?? string.Empty,
            LicenseExpression: (string?)license?.Attribute("type") == "expression" ? license!.Value : null,
            Readme: Value("readme"),
            Icon: Value("icon"),
            Dependencies: dependencies,
            DependencyGroupFrameworks: groupFrameworks,
            Files: zip.Entries.Select(e => e.FullName).ToList(),
            Path: path);
    }

    /// <summary>Reads one file from a package as text.</summary>
    internal static string ReadFile(PackedPackage package, string entryPath)
    {
        using ZipArchive zip = ZipFile.OpenRead(package.Path);
        ZipArchiveEntry entry = zip.GetEntry(entryPath)
            ?? throw new InvalidOperationException($"{package.Id} does not contain {entryPath}.");
        using StreamReader reader = new(entry.Open());
        return reader.ReadToEnd();
    }
}
