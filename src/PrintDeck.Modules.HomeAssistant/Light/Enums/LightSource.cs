namespace PrintDeck.Modules.HomeAssistant.Light.Enums;

/// <summary>Who switched the light.</summary>
public enum LightSource
{
	/// <summary>Someone pressed the light button.</summary>
	Manual,

	/// <summary>The light automation, for a print or a timelapse.</summary>
	Automation
}
