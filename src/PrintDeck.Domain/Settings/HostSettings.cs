using System.Text.Json.Serialization;

namespace PrintDeck.Domain.Settings;

/// <summary>Everything PrintDeck keeps in <c>settings.json</c>: the saved printer profiles plus host-wide settings.</summary>
public sealed record HostSettings
{
	public string ActiveProfileId { get; init; } = PrinterProfile.DefaultId;
	public IReadOnlyList<PrinterProfile> Profiles { get; init; } = [new PrinterProfile()];

	public int DefaultBaudRate { get; init; } = 115200;
	public string? LastPort { get; init; }
	public string? WebcamStreamUrl { get; init; }
	public string ApiKey { get; init; } = "";
	/// <summary>Send M73 progress and M117 messages to the printer's screen during streamed prints.</summary>
	public bool ShowProgressOnPrinter { get; init; } = true;

	/// <summary>Record every serial line in logs/serial.log.</summary>
	public bool SerialLogEnabled { get; init; }

	public string CancelGcode { get; init; } = "M104 S0\nM140 S0\nM107\nG91\nG1 Z10 F600\nG90\nM84";
	public IReadOnlyList<TemperaturePreset> Presets { get; init; } =
	[
		new("PLA", 205, 60),
		new("PETG", 240, 80),
		new("ABS", 250, 100)
	];

	/// <summary>The profile in use; falls back to the first one if the active id is stale.</summary>
	[JsonIgnore]
	public PrinterProfile ActiveProfile =>
		Profiles.FirstOrDefault(p => p.Id == ActiveProfileId) ?? Profiles.FirstOrDefault() ?? new PrinterProfile();

	public PrinterProfile? FindProfile(string id) => Profiles.FirstOrDefault(p => p.Id == id);

	/// <summary>Repairs a hand-edited or older file: at least one profile, unique ids, and an active id that exists.</summary>
	public HostSettings Normalized()
	{
		List<PrinterProfile> profiles = [];
		HashSet<string> ids = [];
		foreach (PrinterProfile p in Profiles ?? [])
		{
			PrinterProfile fixedId = string.IsNullOrWhiteSpace(p.Id) || !ids.Add(p.Id) ? p with { Id = PrinterProfile.NewId() } : p;
			ids.Add(fixedId.Id);
			profiles.Add(fixedId);
		}
		if (profiles.Count == 0) profiles.Add(new PrinterProfile());

		string active = profiles.Any(p => p.Id == ActiveProfileId) ? ActiveProfileId : profiles[0].Id;
		return this with { Profiles = profiles, ActiveProfileId = active, Presets = Presets ?? [], CancelGcode = CancelGcode ?? "" };
	}
}
