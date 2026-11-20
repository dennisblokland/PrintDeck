using PrintDeck.Plugins;

namespace PrintDeck.Application.Plugins;

/// <summary>The modules PrintDeck was built with, as listed in <c>Program.cs</c>.</summary>
public sealed class ModuleCatalog(IEnumerable<IPrintDeckModule> modules)
{
	public const string Unknown = "There is no such module.";

	public IReadOnlyList<IPrintDeckModule> All { get; } = modules.OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

	public IPrintDeckModule? Find(string id) => All.FirstOrDefault(m => m.Id == id);
}
