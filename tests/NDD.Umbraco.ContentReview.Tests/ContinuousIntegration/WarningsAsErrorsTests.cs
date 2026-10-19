using NDD.Umbraco.ContentReview.Tests.Infrastructure;

namespace NDD.Umbraco.ContentReview.Tests.ContinuousIntegration;

/// <summary>
/// Directory.Build.props makes warnings errors when CI=true, and only then. Checked by building a
/// probe project with a nullable warning inside the clean copy, so the repository's own
/// Directory.Build.props is what applies.
/// </summary>
public sealed class WarningsAsErrorsTests(PackedPackages packed)
{
    private ProcessResult BuildProbe(string? ci)
    {
        string probe = Path.Combine(packed.CopyDirectory, "warning-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(probe);
        File.WriteAllText(Path.Combine(probe, "Probe.csproj"),
            """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><IsPackable>false</IsPackable></PropertyGroup></Project>""");
        // CS8602: dereference of a possibly null reference
        File.WriteAllText(Path.Combine(probe, "Probe.cs"),
            "namespace Probe; public static class P { public static int Length(string? s) => s.Length; }");

        return ProcessRunner.Run(
            ProcessRunner.DotNet,
            ["build", Path.Combine(probe, "Probe.csproj"), "-nologo", "--artifacts-path", Path.Combine(probe, "artifacts")],
            packed.CopyDirectory,
            // Set explicitly either way, so the result doesn't depend on whether this run is itself in CI
            new Dictionary<string, string?> { ["CI"] = ci, ["TF_BUILD"] = null });
    }

    [Fact]
    public void A_nullable_warning_fails_the_build_when_CI_is_true()
    {
        ProcessResult result = BuildProbe("true");

        Assert.False(result.Succeeded, "The build succeeded with a nullable warning under CI=true.\n" + result.Output);
        Assert.Contains("error CS8602", result.Output);
    }

    [Fact]
    public void A_nullable_warning_stays_a_warning_locally()
    {
        ProcessResult result = BuildProbe(null);

        Assert.True(result.Succeeded, result.Output);
        Assert.Contains("warning CS8602", result.Output);
    }
}
