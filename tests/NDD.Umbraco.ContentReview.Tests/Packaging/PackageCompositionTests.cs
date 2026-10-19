using System.Text.Json;
using NDD.Umbraco.ContentReview.Tests.Infrastructure;

[assembly: AssemblyFixture(typeof(PackedPackages))]

namespace NDD.Umbraco.ContentReview.Tests.Packaging;

/// <summary>
/// The package-composition spec, checked against nuspecs packed from a clean copy of the
/// repository rather than against project files or prose.
/// </summary>
public sealed class PackageCompositionTests(PackedPackages packed)
{
    private const string Aggregate = "NDD.Umbraco.ContentReview";
    private const string Core = "NDD.Umbraco.ContentReview.Core";
    private const string Backoffice = "NDD.Umbraco.ContentReview.Backoffice";
    private const string Automate = "NDD.Umbraco.ContentReview.Automate";
    private const string OurPrefix = "NDD.Umbraco.ContentReview";

    private static bool IsOurs(string id) =>
        id.Equals(OurPrefix, StringComparison.OrdinalIgnoreCase)
        || id.StartsWith(OurPrefix + ".", StringComparison.OrdinalIgnoreCase);

    private static bool IsAutomate(string id) =>
        id.StartsWith("Umbraco.Automate", StringComparison.OrdinalIgnoreCase);

    private static bool IsUmbraco(string id) =>
        id.StartsWith("Umbraco.", StringComparison.OrdinalIgnoreCase);

    /// <summary>Every package id reachable from <paramref name="id"/>, following only our own packages' nuspecs.</summary>
    private HashSet<string> Closure(string id)
    {
        HashSet<string> reached = new(StringComparer.OrdinalIgnoreCase);
        Queue<string> queue = new([id]);

        while (queue.TryDequeue(out string? current))
        {
            if (!packed.Packages.TryGetValue(current, out PackedPackage? package))
            {
                continue;
            }

            foreach (PackageDependency dependency in package.Dependencies)
            {
                if (reached.Add(dependency.Id))
                {
                    queue.Enqueue(dependency.Id);
                }
            }
        }

        return reached;
    }

    [Fact]
    public void Aggregate_reaches_core_and_backoffice()
    {
        HashSet<string> closure = Closure(Aggregate);

        Assert.Contains(Core, closure);
        Assert.Contains(Backoffice, closure);
    }

    [Fact]
    public void Aggregate_reaches_every_packable_library_except_automate()
    {
        HashSet<string> closure = Closure(Aggregate);

        string[] unreached = packed.Packages.Keys
            .Where(id => !id.Equals(Aggregate, StringComparison.OrdinalIgnoreCase))
            .Where(id => !id.Equals(Automate, StringComparison.OrdinalIgnoreCase))
            .Where(id => !closure.Contains(id))
            .ToArray();

        Assert.True(unreached.Length == 0,
            $"The {Aggregate} package does not reach: {string.Join(", ", unreached)}. A site installing it would silently lack them.");
    }

    [Fact]
    public void Aggregate_does_not_bring_automate()
    {
        string[] automate = Closure(Aggregate)
            .Where(id => id.Equals(Automate, StringComparison.OrdinalIgnoreCase) || IsAutomate(id))
            .ToArray();

        Assert.True(automate.Length == 0,
            $"The {Aggregate} package reaches {string.Join(", ", automate)}; Umbraco Automate must stay optional.");
    }

    [Fact]
    public void Only_the_automate_package_reaches_umbraco_automate()
    {
        string[] offenders = packed.Packages.Keys
            .Where(id => !id.Equals(Automate, StringComparison.OrdinalIgnoreCase))
            .Where(id => Closure(id).Any(d => IsAutomate(d) || d.Equals(Automate, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.True(offenders.Length == 0,
            $"Only {Automate} may depend on Umbraco Automate, but these packages reach it: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Automate_package_depends_on_core_and_automate_17()
    {
        PackedPackage automate = packed.Get(Automate);

        Assert.Contains(automate.Dependencies, d => d.Id.Equals(Core, StringComparison.OrdinalIgnoreCase));

        PackageDependency automateCore = Assert.Single(automate.Dependencies, d => d.Id.Equals("Umbraco.Automate.Core", StringComparison.OrdinalIgnoreCase));
        Assert.StartsWith("[17.", automateCore.Range);
        Assert.True(NuGetRange.ExcludesUmbraco18(automateCore.Range), $"Umbraco.Automate.Core range {automateCore.Range} admits 18.x");
    }

    [Fact]
    public void No_umbraco_dependency_can_resolve_to_18()
    {
        string[] unbounded = packed.Packages.Values
            .SelectMany(p => p.Dependencies.Where(d => IsUmbraco(d.Id)).Select(d => (Package: p.Id, Dependency: d)))
            .Where(x => !NuGetRange.ExcludesUmbraco18(x.Dependency.Range))
            .Select(x => $"{x.Package} -> {x.Dependency.Id} {x.Dependency.Range}")
            .ToArray();

        Assert.True(unbounded.Length == 0,
            $"These Umbraco dependencies could resolve to Umbraco 18: {string.Join("; ", unbounded)}");

        // A package with no Umbraco dependency at all would pass the check above vacuously
        Assert.Contains(packed.Packages.Values, p => p.Dependencies.Any(d => IsUmbraco(d.Id)));
    }

    [Fact]
    public void Every_package_version_tracks_umbraco_17()
    {
        Assert.NotEmpty(packed.Packages);
        Assert.All(packed.Packages.Values, p => Assert.StartsWith("17.", p.Version));
    }

    [Fact]
    public void Every_package_targets_net10_only()
    {
        Assert.All(packed.Packages.Values, p =>
        {
            Assert.NotEmpty(p.DependencyGroupFrameworks);
            Assert.All(p.DependencyGroupFrameworks, tfm => Assert.Equal("net10.0", tfm));

            string[] libFolders = p.Files
                .Where(f => f.StartsWith("lib/", StringComparison.Ordinal))
                .Select(f => f.Split('/')[1])
                .Distinct()
                .ToArray();
            Assert.All(libFolders, folder => Assert.Equal("net10.0", folder));
        });

        // Every library (everything but the code-less aggregate) ships an assembly
        Assert.All(packed.Packages.Values.Where(p => !p.Id.Equals(Aggregate, StringComparison.OrdinalIgnoreCase)),
            p => Assert.Contains($"lib/net10.0/{p.Id}.dll", p.Files));
    }

    /// <summary>
    /// The package-composition spec's table: each third-party dependency and the packages that may
    /// declare it. Changing this list means changing that requirement.
    /// </summary>
    private static readonly (Func<string, bool> Matches, string Name, string[] AllowedIn)[] AllowedThirdParty =
    [
        (id => id.StartsWith("Umbraco.Cms.", StringComparison.OrdinalIgnoreCase), "Umbraco.Cms.*", [Core, Backoffice]),
        (id => id.Equals("Umbraco.Automate.Core", StringComparison.OrdinalIgnoreCase), "Umbraco.Automate.Core", [Automate]),
        (id => id.Equals("System.Security.Cryptography.Pkcs", StringComparison.OrdinalIgnoreCase), "System.Security.Cryptography.Pkcs", [Core]),
        (id => id.Equals("MessagePack", StringComparison.OrdinalIgnoreCase), "MessagePack", [Backoffice, Automate]),
        (id => id.Equals("OpenTelemetry.Api", StringComparison.OrdinalIgnoreCase), "OpenTelemetry.Api", [Automate]),
    ];

    [Fact]
    public void Packages_declare_only_listed_third_party_dependencies()
    {
        // Not vacuous: the packages do declare third-party dependencies for this to judge
        Assert.Contains(packed.Packages.Values, p => p.Dependencies.Any(d => !IsOurs(d.Id)));

        string[] unlisted = packed.Packages.Values
            .SelectMany(p => p.Dependencies.Where(d => !IsOurs(d.Id)).Select(d => (Package: p.Id, d.Id)))
            .Where(x => !AllowedThirdParty.Any(a => a.Matches(x.Id) && a.AllowedIn.Contains(x.Package, StringComparer.OrdinalIgnoreCase)))
            .Select(x => $"{x.Package} -> {x.Id}")
            .Distinct()
            .ToArray();

        Assert.True(unlisted.Length == 0,
            "These dependencies are not listed for their package in the package-composition spec " +
            $"(name them there, with a licence, before adding them): {string.Join("; ", unlisted)}");
    }

    [Fact]
    public void Every_package_carries_the_shared_metadata()
    {
        Assert.Single(packed.Packages.Values.Select(p => p.Version).Distinct());

        Assert.All(packed.Packages.Values, p =>
        {
            Assert.Equal("MIT", p.LicenseExpression);
            Assert.Equal("Norwood Design & Development Ltd.", p.Authors);
            Assert.Equal("README.md", p.Readme);
            Assert.Equal("icon.png", p.Icon);
            Assert.Contains("README.md", p.Files);
            Assert.Contains("icon.png", p.Files);
            Assert.False(string.IsNullOrWhiteSpace(p.Description), $"{p.Id} has no description");
        });
    }

    [Fact]
    public void No_two_packages_share_a_description()
    {
        var duplicates = packed.Packages.Values
            .GroupBy(p => p.Description)
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(", ", g.Select(p => p.Id)))
            .ToArray();

        Assert.True(duplicates.Length == 0, $"These packages share a description: {string.Join("; ", duplicates)}");
    }

    [Fact]
    public void Only_the_expected_packages_are_produced()
    {
        // TestSite and test projects must not pack; everything that does pack is one of ours
        Assert.All(packed.Packages.Keys, id => Assert.True(IsOurs(id), $"Unexpected package {id}"));
        Assert.DoesNotContain(packed.Packages.Keys, id => id.EndsWith(".TestSite", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(packed.Packages.Keys, id => id.Contains(".Tests", StringComparison.OrdinalIgnoreCase));

        foreach (string expected in new[] { Aggregate, Core, Backoffice, Automate })
        {
            Assert.Contains(expected, packed.Packages.Keys);
        }
    }

    [Fact]
    public void Backoffice_package_contains_the_client_named_by_its_manifest()
    {
        PackedPackage backoffice = packed.Get(Backoffice);
        const string manifestPath = "staticwebassets/App_Plugins/NDDContentReview/umbraco-package.json";

        Assert.Contains(manifestPath, backoffice.Files);

        using JsonDocument manifest = JsonDocument.Parse(PackedPackages.ReadFile(backoffice, manifestPath));
        string[] scripts = manifest.RootElement.GetProperty("extensions")
            .EnumerateArray()
            .Where(e => e.TryGetProperty("js", out _))
            .Select(e => e.GetProperty("js").GetString()!)
            .ToArray();

        Assert.NotEmpty(scripts);
        Assert.All(scripts, js => Assert.Contains("staticwebassets" + js, backoffice.Files));
    }

    [Fact]
    public void Backoffice_manifest_carries_the_package_version()
    {
        PackedPackage backoffice = packed.Get(Backoffice);

        using JsonDocument manifest = JsonDocument.Parse(
            PackedPackages.ReadFile(backoffice, "staticwebassets/App_Plugins/NDDContentReview/umbraco-package.json"));

        Assert.Equal(backoffice.Version, manifest.RootElement.GetProperty("version").GetString());
        Assert.Equal("Content Review", manifest.RootElement.GetProperty("name").GetString());
    }
}
