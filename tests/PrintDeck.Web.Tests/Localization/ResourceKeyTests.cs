using System.Text.RegularExpressions;
using System.Xml.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using PrintDeck.Web.Components.Account;
using PrintDeck.Web.Services;

namespace PrintDeck.Web.Tests.Localization;

/// <summary>
/// UI text lives in <c>Resources/**/*.resx</c>. A missing key doesn't fail anywhere (the localizer shows the key itself),
/// so these tests compare the keys the code uses with the keys in the resource files, both ways.
/// </summary>
public sealed partial class ResourceKeyTests
{
	private static readonly string _web = FindWebProject();
	private static readonly string _resources = Path.Combine(_web, "Resources");
	private static readonly string _sharedResx = Path.Combine(_resources, "SharedText.resx");

	/// <summary>A component's own text: <c>L["Key"]</c>, <c>L["Key", args]</c> or <c>L.Html("KeyHtml", args)</c>.</summary>
	[GeneratedRegex(@"\bL(?:\[\s*|\.Html\(\s*)""(\w+)""")]
	private static partial Regex OwnKey();

	[GeneratedRegex(@"\b[Ss]hared(?:\[\s*|\.Html\(\s*)""(\w+)""")]
	private static partial Regex SharedKey();

	[GeneratedRegex(@"\.Html\(\s*""(\w+)""")]
	private static partial Regex HtmlKey();

	/// <summary><c>Format</c> reads its words with <c>T("Key")</c>.</summary>
	[GeneratedRegex(@"\bT\(\s*""(\w+)""")]
	private static partial Regex FormatKey();

	/// <summary><c>AccountText</c> has one property per message.</summary>
	[GeneratedRegex("""public static string (\w+) => Get\(\);""")]
	private static partial Regex AccountTextKey();

	private sealed record Usage(Dictionary<string, HashSet<string>> KeysByResx, HashSet<string> HtmlKeys);

	private static Usage Scan()
	{
		Dictionary<string, HashSet<string>> byResx = new(StringComparer.Ordinal);
		HashSet<string> html = [];
		void Use(string resx, string key) => (byResx.TryGetValue(resx, out HashSet<string>? keys) ? keys : byResx[resx] = []).Add(key);

		foreach (string file in Directory.EnumerateFiles(_web, "*.*", SearchOption.AllDirectories)
			.Where(f => f.EndsWith(".razor", StringComparison.Ordinal) || f.EndsWith(".cs", StringComparison.Ordinal))
			.Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !f.Contains("node_modules", StringComparison.Ordinal)))
		{
			string source = File.ReadAllText(file);
			string relative = Path.GetRelativePath(_web, file);
			string own = Path.Combine(_resources, Path.ChangeExtension(relative, ".resx"));

			foreach (Match m in OwnKey().Matches(source)) Use(own, m.Groups[1].Value);
			foreach (Match m in SharedKey().Matches(source)) Use(_sharedResx, m.Groups[1].Value);
			foreach (Match m in HtmlKey().Matches(source)) html.Add(m.Groups[1].Value);
			foreach (Match m in FormatKey().Matches(source)) Use(own, m.Groups[1].Value);
			foreach (Match m in AccountTextKey().Matches(source)) Use(own, m.Groups[1].Value);
		}
		return new Usage(byResx, html);
	}

	private static Dictionary<string, string> Read(string resx) =>
		XDocument.Load(resx).Root!.Elements("data")
			.ToDictionary(d => (string) d.Attribute("name")!, d => (string?) d.Element("value") ?? "");

	private static IEnumerable<string> AllResx() => Directory.EnumerateFiles(_resources, "*.resx", SearchOption.AllDirectories);

	[Fact]
	public void Every_key_the_ui_uses_is_in_its_resource_file()
	{
		List<string> missing = [];
		foreach ((string resx, HashSet<string> keys) in Scan().KeysByResx)
		{
			Dictionary<string, string> entries = File.Exists(resx) ? Read(resx) : [];
			missing.AddRange(keys.Where(k => !entries.ContainsKey(k)).Select(k => $"{Path.GetRelativePath(_web, resx)}: {k}"));
		}
		Assert.Empty(missing);
	}

	[Fact]
	public void Every_key_in_a_resource_file_is_used()
	{
		Dictionary<string, HashSet<string>> used = Scan().KeysByResx;
		List<string> unused = [];
		foreach (string resx in AllResx())
		{
			HashSet<string> keys = used.GetValueOrDefault(resx) ?? [];
			unused.AddRange(Read(resx).Keys.Where(k => !keys.Contains(k)).Select(k => $"{Path.GetRelativePath(_web, resx)}: {k}"));
		}
		Assert.Empty(unused);
	}

	[Fact]
	public void Only_keys_ending_in_html_hold_markup_and_only_they_are_rendered_as_markup()
	{
		HashSet<string> html = Scan().HtmlKeys;
		List<string> wrong = [];
		foreach (string resx in AllResx())
			foreach ((string key, string value) in Read(resx))
			{
				bool isHtml = key.EndsWith("Html", StringComparison.Ordinal);
				if (!isHtml && value.Contains('<')) wrong.Add($"{key} holds markup; name it {key}Html and render it with L.Html");
				if (isHtml != html.Contains(key)) wrong.Add($"{key}: keys ending in Html are rendered with L.Html, and only those");
			}
		Assert.Empty(wrong);
	}

	[Fact]
	public async Task The_app_finds_every_component_text()
	{
		await using PrintDeckWebFactory app = new();
		IStringLocalizerFactory factory = app.Services.GetRequiredService<IStringLocalizerFactory>();
		List<string> problems = [];
		foreach (string resx in AllResx())
		{
			string typeName = "PrintDeck.Web." + Path.ChangeExtension(Path.GetRelativePath(_resources, resx), null)!.Replace(Path.DirectorySeparatorChar, '.');
			if (typeof(SharedText).Assembly.GetType(typeName) is not { } type)
			{
				problems.Add($"{Path.GetRelativePath(_web, resx)}: no type {typeName}");
				continue;
			}
			IStringLocalizer localizer = factory.Create(type);
			foreach ((string key, string value) in Read(resx))
			{
				LocalizedString found = localizer[key];
				if (found.ResourceNotFound || found.Value != value) problems.Add($"{typeName}: {key}");
			}
		}
		Assert.Empty(problems);
	}

	[Fact]
	public void Format_and_the_account_messages_read_their_resources()
	{
		DateTimeOffset now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
		Assert.Equal("just now", Format.Ago(now, now));
		Assert.Equal("Choose a username.", AccountText.ChooseUsername);
		Assert.Equal("The passwords don't match.", AccountText.PasswordsDiffer);
	}

	[Fact]
	public async Task Html_resources_encode_their_arguments()
	{
		await using PrintDeckWebFactory app = new();
		IStringLocalizer localizer = app.Services.GetRequiredService<IStringLocalizerFactory>().Create(typeof(Components.Pages.Timelapse));

		Assert.Equal("Recording <strong>&lt;b&gt;cube&amp;co.gcode</strong>: 3 frames",
			localizer.Html("RecordingHtml", "<b>cube&co.gcode", "3 frames").Value);
	}

	private static string FindWebProject()
	{
		for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
		{
			string candidate = Path.Combine(dir.FullName, "src", "PrintDeck.Web");
			if (File.Exists(Path.Combine(candidate, "PrintDeck.Web.csproj"))) return candidate;
		}
		throw new DirectoryNotFoundException("src/PrintDeck.Web not found above " + AppContext.BaseDirectory);
	}
}
