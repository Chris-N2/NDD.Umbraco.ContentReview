using System.Text.RegularExpressions;
using NDD.Umbraco.ContentReview.Tests.Infrastructure;

namespace NDD.Umbraco.ContentReview.Tests.ContinuousIntegration;

/// <summary>
/// The CI workflow: triggers, warnings-as-errors, permissions, and pinned actions. Line scans, not a YAML parser: enough
/// while the only workflow is one we write by hand (design D7). Replace it when that changes.
/// </summary>
public sealed partial class WorkflowTests
{
    [GeneratedRegex(@"^\s*(-\s*)?uses:\s*(?<ref>[^\s#]+)", RegexOptions.Multiline)]
    private static partial Regex UsesLine();

    [GeneratedRegex(@"^[^@]+@[0-9a-f]{40}$")]
    private static partial Regex PinnedToSha();

    private static string WorkflowsDirectory => Path.Combine(Repository.Root, ".github", "workflows");

    private static string[] Workflows() =>
        Directory.GetFiles(WorkflowsDirectory, "*.yml")
            .Concat(Directory.GetFiles(WorkflowsDirectory, "*.yaml"))
            .ToArray();

    private static string CiWorkflow => File.ReadAllText(Path.Combine(WorkflowsDirectory, "ci.yml"));

    [Fact]
    public void Every_action_is_pinned_to_a_commit_sha()
    {
        string[] workflows = Workflows();
        Assert.NotEmpty(workflows);

        List<string> unpinned = [];
        int checkedCount = 0;

        foreach (string workflow in workflows)
        {
            foreach (Match match in UsesLine().Matches(File.ReadAllText(workflow)))
            {
                string reference = match.Groups["ref"].Value.Trim('\'', '"');

                // Local actions ("./path") and docker images are not third-party git refs
                if (reference.StartsWith("./", StringComparison.Ordinal) || reference.StartsWith("docker://", StringComparison.Ordinal))
                {
                    continue;
                }

                checkedCount++;
                if (!PinnedToSha().IsMatch(reference))
                {
                    unpinned.Add($"{Path.GetFileName(workflow)}: {reference}");
                }
            }
        }

        Assert.True(checkedCount > 0, "No `uses:` lines were found; the scan is not seeing the workflows.");
        Assert.True(unpinned.Count == 0, $"Actions not pinned to a full commit SHA: {string.Join("; ", unpinned)}");
    }

    [Fact]
    public void Ci_runs_on_every_push_and_on_pull_requests_to_main()
    {
        string ci = CiWorkflow;

        Assert.Matches(new Regex(@"^on:\s*\r?\n\s+push:\s*\r?\n\s+branches:\s*\[\s*'\*\*'\s*\]\s*$", RegexOptions.Multiline), ci);
        Assert.Matches(new Regex(@"^\s+pull_request:\s*\r?\n\s+branches:\s*\[\s*main\s*\]\s*$", RegexOptions.Multiline), ci);
    }

    [Fact]
    public void Ci_builds_with_warnings_as_errors_from_a_clean_checkout()
    {
        string ci = CiWorkflow;

        // CI=true is what Directory.Build.props turns into TreatWarningsAsErrors
        Assert.Matches(new Regex(@"^\s+CI:\s*'true'\s*$", RegexOptions.Multiline), ci);
        Assert.Matches(new Regex(@"dotnet build NDD\.Umbraco\.ContentReview\.slnx"), ci);
        Assert.Matches(new Regex(@"Assert-TestResults\.ps1"), ci);
        Assert.Contains("TreatWarningsAsErrors Condition=\"'$(CI)' == 'true'",
            File.ReadAllText(Path.Combine(Repository.Root, "Directory.Build.props")));
    }

    [Fact]
    public void Workflows_are_read_only()
    {
        string[] workflows = Workflows();
        Assert.NotEmpty(workflows);

        foreach (string workflow in workflows)
        {
            string text = File.ReadAllText(workflow);

            Assert.Matches(new Regex(@"^permissions:\s*\r?\n\s+contents:\s*read\s*$", RegexOptions.Multiline), text);
            Assert.DoesNotMatch(new Regex(@":\s*write\b"), text);
        }
    }
}
