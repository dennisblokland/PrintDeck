namespace PrintDeck.Domain.Temperatures;

/// <summary>One heater in a temperature report. Heater is "T0", "T1", "B" or "C".</summary>
public sealed record HeaterReading(string Heater, double Actual, double Target)
{
	public bool IsBed => Heater == "B";
	public bool IsChamber => Heater == "C";
	public string DisplayName => Heater switch
	{
		"B" => "Bed",
		"C" => "Chamber",
		"T" or "T0" => "Hotend",
		string t when t.StartsWith('T') => $"Hotend {t[1..]}",
		string other => other
	};
}
