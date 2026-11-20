namespace PrintDeck.Plugins.Enums;

/// <summary>Places in PrintDeck's pages where a module's components appear.</summary>
public enum ModuleSlot
{
	/// <summary>The module's card in Settings → Modules, shown while the module is on.</summary>
	Settings,

	/// <summary>A card on the dashboard.</summary>
	Dashboard,

	/// <summary>A card on the Control page.</summary>
	Control,

	/// <summary>A small button in the header of the dashboard's camera card.</summary>
	CameraCard
}
