using System.Text.Json.Serialization;

using PrintDeck.Modules.HomeAssistant.Client;
using PrintDeck.Plugins;

namespace PrintDeck.Modules.HomeAssistant;

/// <summary>The module's part of <c>settings.json</c>.</summary>
public sealed class HomeAssistantSettings
{
	/// <summary>The URL that gives an in-process Home Assistant with a printer light, for trying the module without one.</summary>
	public const string Virtual = "VIRTUAL";

	public const int MaxOffDelayMinutes = 120;

	/// <summary>Home Assistant's address, e.g. <c>http://homeassistant.local:8123</c>, or <see cref="Virtual"/>.</summary>
	public string Url { get; set; } = "";

	/// <summary>A long-lived access token, made in the Home Assistant profile.</summary>
	[Secret]
	public string Token { get; set; } = "";

	/// <summary>The printer light: a <c>light.*</c> or <c>switch.*</c> entity.</summary>
	public string LightEntity { get; set; } = "";

	/// <summary>Switch the light on when a print starts and off a while after it ends.</summary>
	public bool LightWhilePrinting { get; set; }

	/// <summary>Switch the light on when a timelapse starts recording and off a while after it is done.</summary>
	public bool LightForTimelapse { get; set; }

	/// <summary>How long the light stays on after the print or timelapse it was on for.</summary>
	public int LightOffDelayMinutes { get; set; } = 3;

	[JsonIgnore]
	public bool IsVirtual => Url.Trim().Equals(Virtual, StringComparison.OrdinalIgnoreCase);

	[JsonIgnore]
	public bool HasLight => LightEntity.Length > 0;

	/// <summary>What is wrong with these settings, as a sentence for the user; null when they can be saved.</summary>
	public string? Problem() =>
		Url.Length > 0 && !IsVirtual && !(Uri.TryCreate(Url, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https")
			? "The Home Assistant URL must start with http:// or https://, or be VIRTUAL."
		: LightEntity.Length > 0 && !EntityId.IsSwitchable(LightEntity)
			? "The light must be a light.* or switch.* entity."
		: LightOffDelayMinutes is < 0 or > MaxOffDelayMinutes
			? $"Switch the light off 0 to {MaxOffDelayMinutes} minutes after."
		: null;

	/// <summary>A copy with the text fields trimmed.</summary>
	public HomeAssistantSettings Trimmed() => new()
	{
		Url = Url.Trim(),
		Token = Token.Trim(),
		LightEntity = LightEntity.Trim(),
		LightWhilePrinting = LightWhilePrinting,
		LightForTimelapse = LightForTimelapse,
		LightOffDelayMinutes = LightOffDelayMinutes
	};
}
