using System.Xml.Linq;
using NDD.Umbraco.ContentReview.Tests.Infrastructure;

namespace NDD.Umbraco.ContentReview.Tests.Packaging;

/// <summary>
/// The Automate isolation rule at source level, so a violation fails before anyone packs.
/// </summary>
public sealed class ProjectGraphTests
{
    private const string AutomateProject = "NDD.Umbraco.ContentReview.Automate";

    // May reference Automate: the integration itself, and the dev site that exercises it
    private static readonly string[] Allowed = [AutomateProject, "NDD.Umbraco.ContentReview.TestSite"];

    private sealed record Project(string Name, string Path, IReadOnlyList<string> PackageReferences, IReadOnlyList<string> ProjectReferences);

    private static Dictionary<string, Project> LoadProjects() =>
        Directory.GetFiles(Repository.SourceDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Split(System.IO.Path.DirectorySeparatorChar).Any(s => s is "bin" or "obj" or "node_modules"))
            .Select(Load)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    private static Project Load(string path)
    {
        XDocument xml = XDocument.Load(path);
        string directory = System.IO.Path.GetDirectoryName(path)!;

        List<string> packages = xml.Descendants()
            .Where(e => e.Name.LocalName == "PackageReference")
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .ToList();

        List<string> projects = xml.Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => System.IO.Path.GetFileNameWithoutExtension(
                System.IO.Path.GetFullPath(System.IO.Path.Combine(directory, ((string?)e.Attribute("Include") ?? string.Empty).Replace('\\', System.IO.Path.DirectorySeparatorChar)))))
            .ToList();

        return new Project(System.IO.Path.GetFileNameWithoutExtension(path), path, packages, projects);
    }

    /// <summary>True when the project, or any project it references, references Automate.</summary>
    private static bool ReachesAutomate(Project project, IReadOnlyDictionary<string, Project> all, HashSet<string> visited)
    {
        if (!visited.Add(project.Name))
        {
            return false;
        }

        if (project.PackageReferences.Any(p => p.StartsWith("Umbraco.Automate", StringComparison.OrdinalIgnoreCase))
            || project.ProjectReferences.Any(p => p.Equals(AutomateProject, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return project.ProjectReferences
            .Where(all.ContainsKey)
            .Any(p => ReachesAutomate(all[p], all, visited));
    }

    [Fact]
    public void Only_the_automate_project_and_testsite_reference_automate()
    {
        Dictionary<string, Project> projects = LoadProjects();
        Assert.Contains(AutomateProject, projects.Keys);

        string[] offenders = projects.Values
            .Where(p => !Allowed.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
            .Where(p => ReachesAutomate(p, projects, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
            .Select(p => p.Name)
            .ToArray();

        Assert.True(offenders.Length == 0,
            $"Only {AutomateProject} may reference Umbraco Automate (CLAUDE.md invariant 3), but these projects reach it: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void The_automate_project_does_reference_automate()
    {
        // Guards the test above against passing because the reference moved or was renamed
        Dictionary<string, Project> projects = LoadProjects();

        Assert.Contains(projects[AutomateProject].PackageReferences, p => p.StartsWith("Umbraco.Automate", StringComparison.OrdinalIgnoreCase));
    }
}
