using System.Reflection;
using System.Xml.Linq;

using PrintDeck.Plugins;

namespace PrintDeck.Modules.HomeAssistant.Tests;

/// <summary>
/// A module reaches PrintDeck only through <c>PrintDeck.Plugins.Abstractions</c>, and the contract references nothing
/// else of PrintDeck. Then a module could as well be loaded from outside, and the contract has to grow when a module
/// needs more, instead of the module reaching into the session or the stores.
/// </summary>
public sealed class ModuleBoundaryTests
{
	private const string Contract = "PrintDeck.Plugins.Abstractions";

	private static readonly Assembly[] _modules = [typeof(HomeAssistantModule).Assembly];

	private static IEnumerable<string> PrintDeckReferences(Assembly assembly) =>
		assembly.GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("PrintDeck.", StringComparison.Ordinal));

	[Fact]
	public void The_contract_references_nothing_else_of_printdeck()
	{
		Assert.Equal(Contract, typeof(IPluginContext).Assembly.GetName().Name);
		Assert.Empty(PrintDeckReferences(typeof(IPluginContext).Assembly));
	}

	[Fact]
	public void Modules_reference_only_the_contract()
	{
		foreach (Assembly module in _modules)
			Assert.All(PrintDeckReferences(module), name => Assert.Equal(Contract, name));
	}

	[Fact]
	public void Module_projects_reference_only_the_contract_project()
	{
		string src = Path.Combine(RepoRoot(), "src");
		string[] projects = Directory.GetFiles(src, "PrintDeck.Modules.*.csproj", SearchOption.AllDirectories);
		Assert.NotEmpty(projects);
		foreach (string project in projects)
		{
			IEnumerable<string> references = XDocument.Load(project).Descendants("ProjectReference")
				.Select(r => Path.GetFileNameWithoutExtension(((string) r.Attribute("Include")!).Replace('\\', '/')));
			Assert.All(references, r => Assert.Equal(Contract, r));
		}
	}

	private static string RepoRoot()
	{
		for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
			if (File.Exists(Path.Combine(dir.FullName, "PrintDeck.slnx"))) return dir.FullName;
		throw new DirectoryNotFoundException("PrintDeck.slnx not found above the test output.");
	}
}
