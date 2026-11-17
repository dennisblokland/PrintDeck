using System.Text.RegularExpressions;

using PrintDeck.Domain.Common;

namespace PrintDeck.Domain.Settings;

/// <summary>
/// Expands a G-code script: a line <c>{% snippet 'name' %}</c> (OctoPrint's syntax, single or double quotes)
/// is replaced by that snippet's lines; snippets may include other snippets.
/// </summary>
public static partial class GcodeScript
{
	private const int MaxDepth = 10;

	[GeneratedRegex("""^\s*\{%\s*snippet\s+(?:'([^']*)'|"([^"]*)")\s*%\}\s*$""")]
	private static partial Regex SnippetLine();

	[GeneratedRegex("^[A-Za-z0-9_-]+$")]
	private static partial Regex ValidName();

	public static bool IsValidSnippetName(string name) => ValidName().IsMatch(name);

	/// <summary>The script's lines with every snippet filled in; fails on an unknown snippet or one that includes itself.</summary>
	public static Result<IReadOnlyList<string>> Expand(string script, IReadOnlyList<GcodeSnippet> snippets)
	{
		List<string> lines = [];
		if (string.IsNullOrWhiteSpace(script)) return Result.Success<IReadOnlyList<string>>(lines);
		string? error = Expand(script, snippets, lines, []);
		return error is null ? Result.Success<IReadOnlyList<string>>(lines) : Result.Failure<IReadOnlyList<string>>(error);
	}

	private static string? Expand(string script, IReadOnlyList<GcodeSnippet> snippets, List<string> lines, List<string> including)
	{
		foreach (string raw in script.Replace("\r", "").Split('\n'))
		{
			if (SnippetLine().Match(raw) is not { Success: true } m)
			{
				lines.Add(raw);
				continue;
			}

			string name = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
			GcodeSnippet? snippet = snippets.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
			if (snippet is null) return $"Unknown snippet '{name}'.";
			if (including.Contains(snippet.Name, StringComparer.OrdinalIgnoreCase) || including.Count >= MaxDepth)
				return $"Snippet '{snippet.Name}' includes itself.";

			including.Add(snippet.Name);
			if (Expand(snippet.Gcode, snippets, lines, including) is { } error) return error;
			including.RemoveAt(including.Count - 1);
		}
		return null;
	}
}
