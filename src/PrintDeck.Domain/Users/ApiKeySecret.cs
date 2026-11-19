using System.Security.Cryptography;
using System.Text;

namespace PrintDeck.Domain.Users;

/// <summary>
/// App key secrets. A key is shown once when it is made; only its SHA-256 hash and last four characters are kept,
/// so a copied database does not give away working keys.
/// </summary>
public static class ApiKeySecret
{
	public const int MaxNameLength = 64;

	/// <summary>32 hex characters, the same shape as the shared key, which every slicer accepts.</summary>
	public static string New() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

	public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

	/// <summary>The last four characters, to tell keys apart in the list.</summary>
	public static string Hint(string key) => key.Length <= 4 ? key : key[^4..];

	/// <summary>Why <paramref name="name"/> can't name a key, or null when it can. Names are compared trimmed and ignoring case.</summary>
	public static string? NameProblem(string name, IEnumerable<string> taken)
	{
		string trimmed = name.Trim();
		if (trimmed.Length is 0 or > MaxNameLength) return $"Give the key a name of up to {MaxNameLength} characters.";
		return taken.Any(t => string.Equals(t.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)) ? "A key with that name already exists." : null;
	}

	/// <summary><paramref name="name"/>, or "name (2)", "name (3)"… when a key already has it.</summary>
	public static string FreeName(string name, IReadOnlyCollection<string> taken)
	{
		string trimmed = name.Trim();
		if (trimmed.Length > MaxNameLength - 5) trimmed = trimmed[..(MaxNameLength - 5)].TrimEnd();
		string candidate = trimmed;
		for (int n = 2; NameProblem(candidate, taken) is not null; n++) candidate = $"{trimmed} ({n})";
		return candidate;
	}
}
