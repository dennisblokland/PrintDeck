using PrintDeck.Domain.Files;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Files;

/// <summary>Runs <see cref="GcodeProblemCheck"/> over a whole file.</summary>
public static class GcodeFileChecker
{
	public static async Task<IReadOnlyList<GcodeProblem>> CheckAsync(Stream stream, PrinterProfile profile, CancellationToken ct)
	{
		GcodeProblemCheck check = new(profile);
		using StreamReader reader = new(stream);
		while (await reader.ReadLineAsync(ct) is { } line) check.Inspect(line);
		return check.Finish();
	}
}
