using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using PrintDeck.Modules.HomeAssistant;
using PrintDeck.Web.Components.Account;
using PrintDeck.Web.Services;

namespace PrintDeck.Web.Tests.Localization;

/// <summary>
/// UI text lives in <c>Resources/**/*.resx</c>. A missing key doesn't fail anywhere (the localizer shows the key itself),
/// so these tests compare the keys the code uses with the keys in the resource files, both ways. Modules keep their
/// text the same way, in their own project.
/// </summary>
public sealed partial class ResourceKeyTests
{
	/// <summary>A project with UI text: its folder, its assembly and the namespace its resource names start with.</summary>
	private sealed record UiProject(string Directory, Assembly Assembly, string RootNamespace)
	{
		public string Resources => Path.Combine(Directory, "Resources");
	}

	private static readonly string _web = FindProject("PrintDeck.Web");
	private static readonly string _sharedResx = Path.Combine(_web, "Resources", "SharedText.resx");

	private static readonly UiProject[] _projects =
	[
		new(_web, typeof(SharedText).Assembly, "PrintDeck.Web"),
		new(FindProject("PrintDeck.Modules.HomeAssistant"), typeof(HomeAssistantModule).Assembly, "PrintDeck.Modules.HomeAssistant")
	];

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

		foreach (UiProject project in _projects)
		foreach (string file in Directory.EnumerateFiles(project.Directory, "*.*", SearchOption.AllDirectories)
			.Where(f => f.EndsWith(".razor", StringComparison.Ordinal) || f.EndsWith(".cs", StringComparison.Ordinal))
			.Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !f.Contains("node_modules", StringComparison.Ordinal)))
		{
			string source = File.ReadAllText(file);
			string relative = Path.GetRelativePath(project.Directory, file);
			string own = Path.Combine(project.Resources, Path.ChangeExtension(relative, ".resx"));

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

	private static IEnumerable<string> AllResx() =>
		_projects.SelectMany(p => Directory.EnumerateFiles(p.Resources, "*.resx", SearchOption.AllDirectories));

	private static string Shown(string resx) => Path.GetRelativePath(Path.GetDirectoryName(_web)!, resx);

	[Fact]
	public void Every_key_the_ui_uses_is_in_its_resource_file()
	{
		List<string> missing = [];
		foreach ((string resx, HashSet<string> keys) in Scan().KeysByResx)
		{
			Dictionary<string, string> entries = File.Exists(resx) ? Read(resx) : [];
			missing.AddRange(keys.Where(k => !entries.ContainsKey(k)).Select(k => $"{Shown(resx)}: {k}"));
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
			unused.AddRange(Read(resx).Keys.Where(k => !keys.Contains(k)).Select(k => $"{Shown(resx)}: {k}"));
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
		foreach (UiProject project in _projects)
		foreach (string resx in Directory.EnumerateFiles(project.Resources, "*.resx", SearchOption.AllDirectories))
		{
			string typeName = project.RootNamespace + "." + Path.ChangeExtension(Path.GetRelativePath(project.Resources, resx), null)!.Replace(Path.DirectorySeparatorChar, '.');
			if (project.Assembly.GetType(typeName) is not { } type)
			{
				problems.Add($"{Shown(resx)}: no type {typeName}");
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

	private static string FindProject(string name)
	{
		for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
		{
			string candidate = Path.Combine(dir.FullName, "src", name);
			if (File.Exists(Path.Combine(candidate, name + ".csproj"))) return candidate;
		}
		throw new DirectoryNotFoundException($"src/{name} not found above " + AppContext.BaseDirectory);
	}
}
