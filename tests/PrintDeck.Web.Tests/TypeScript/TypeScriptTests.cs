using System.Diagnostics;

namespace PrintDeck.Web.Tests.TypeScript;

/// <summary>
/// Runs the viewer's TypeScript checks (tsc + Vitest, see src/PrintDeck.Web/package.json) as part of
/// <c>dotnet test</c>. Needs Node.js/npm, like building PrintDeck.Web does.
/// </summary>
public class TypeScriptTests
{
	private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(5);

	[Fact]
	public async Task Viewer_typecheck_and_unit_tests_pass()
	{
		string web = FindWebProject();
		CancellationToken ct = TestContext.Current.CancellationToken;

		if (!Directory.Exists(Path.Combine(web, "node_modules")))
			await RunNpmAsync(web, "ci --no-audit --no-fund", ct);

		await RunNpmAsync(web, "test", ct);
	}

	private static async Task RunNpmAsync(string directory, string args, CancellationToken ct)
	{
		ProcessStartInfo start = OperatingSystem.IsWindows()
			? new("cmd.exe", $"/c npm {args}")
			: new("npm", args);
		start.WorkingDirectory = directory;
		start.RedirectStandardOutput = true;
		start.RedirectStandardError = true;
		start.Environment["CI"] = "1";
		start.Environment["NO_COLOR"] = "1";
		start.Environment["FORCE_COLOR"] = "0";

		using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start npm.");
		Task<string> stdout = process.StandardOutput.ReadToEndAsync(ct);
		Task<string> stderr = process.StandardError.ReadToEndAsync(ct);

		using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeout.CancelAfter(_timeout);
		try
		{
			await process.WaitForExitAsync(timeout.Token);
		}
		catch (OperationCanceledException)
		{
			process.Kill(entireProcessTree: true);
			throw;
		}

		string output = (await stdout) + (await stderr);
		Assert.True(process.ExitCode == 0, $"npm {args} failed with exit code {process.ExitCode}:\n{output}");
	}

	/// <summary>src/PrintDeck.Web, found by walking up from the test binaries.</summary>
	private static string FindWebProject()
	{
		for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
		{
			string candidate = Path.Combine(dir.FullName, "src", "PrintDeck.Web");
			if (File.Exists(Path.Combine(candidate, "package.json"))) return candidate;
		}
		throw new DirectoryNotFoundException("src/PrintDeck.Web/package.json not found above " + AppContext.BaseDirectory);
	}
}
