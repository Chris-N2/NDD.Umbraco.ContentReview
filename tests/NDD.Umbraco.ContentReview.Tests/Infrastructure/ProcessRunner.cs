using System.Diagnostics;
using System.Text;

namespace NDD.Umbraco.ContentReview.Tests.Infrastructure;

internal sealed record ProcessResult(int ExitCode, string Output)
{
    public bool Succeeded => ExitCode == 0;
}

internal static class ProcessRunner
{
    /// <summary>
    /// Runs a process to completion, capturing stdout and stderr together.
    /// </summary>
    public static ProcessResult Run(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environment = null,
        TimeSpan? timeout = null)
    {
        ProcessStartInfo start = new(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // A test run inherits MSBuild state from `dotnet test` that would leak into a nested build.
        start.Environment.Remove("MSBuildExtensionsPath");
        start.Environment.Remove("MSBuildSDKsPath");
        start.Environment.Remove("MSBUILD_EXE_PATH");
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "true";
        start.Environment["DOTNET_NOLOGO"] = "true";

        // No long-lived MSBuild worker nodes or build server: they inherit the redirected output
        // pipes and outlive the build, so end-of-stream never arrives and the test run hangs.
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        start.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        start.Environment["UseSharedCompilation"] = "false";

        if (environment is not null)
        {
            foreach ((string key, string? value) in environment)
            {
                start.Environment[key] = value;
            }
        }

        StringBuilder output = new();
        using Process process = new() { StartInfo = start };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { lock (output) { output.AppendLine(e.Data); } } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { lock (output) { output.AppendLine(e.Data); } } };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(timeout ?? TimeSpan.FromMinutes(15)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{fileName} {string.Join(' ', arguments)} did not finish in time.\n{output}");
        }

        // Flush the async readers, but never wait unbounded: an inherited pipe would hang here
        using (CancellationTokenSource drain = new(TimeSpan.FromSeconds(30)))
        {
            try
            {
                process.WaitForExitAsync(drain.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
        }

        string captured;
        lock (output)
        {
            captured = output.ToString();
        }

        return new ProcessResult(process.ExitCode, captured);
    }

    /// <summary>The dotnet host running this test, so nested builds use the same SDK.</summary>
    public static string DotNet =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host
            ? host
            : "dotnet";
}
