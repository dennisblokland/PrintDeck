using System.Text;

namespace PrintDeck.Domain.SdCard;

/// <summary>
/// Picks the DOS 8.3 name a file gets on the SD card. Marlin only creates 8.3 names with <c>M28</c>,
/// so "Benchy 0.2mm PLA.gcode" becomes "BENCHY02.GCO", and "BENCHY~1.GCO" when that is taken.
/// </summary>
public static class SdShortName
{
	private const string _extension = ".GCO";

	public static string For(string fileName, IEnumerable<string> existing)
	{
		HashSet<string> taken = new(existing.Select(p => p.ToUpperInvariant()), StringComparer.Ordinal);
		string stem = Stem(fileName);

		string candidate = stem + _extension;
		if (!taken.Contains(candidate)) return candidate;

		for (int n = 1; n < 1000; n++)
		{
			string suffix = "~" + n;
			candidate = stem[..Math.Min(stem.Length, 8 - suffix.Length)] + suffix + _extension;
			if (!taken.Contains(candidate)) return candidate;
		}
		throw new InvalidOperationException("No free 8.3 name left for " + fileName);
	}

	/// <summary>Upper-case letters and digits of the name without extension, at most 8; "FILE" when none are left.</summary>
	private static string Stem(string fileName)
	{
		string name = Path.GetFileNameWithoutExtension(fileName);
		StringBuilder stem = new(8);
		foreach (char c in name.ToUpperInvariant())
		{
			if (c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '-') stem.Append(c);
			if (stem.Length == 8) break;
		}
		return stem.Length == 0 ? "FILE" : stem.ToString();
	}
}
