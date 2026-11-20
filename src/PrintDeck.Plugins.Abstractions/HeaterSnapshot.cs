namespace PrintDeck.Plugins;

/// <param name="Heater">"T0", "T1", "B" (bed) or "C" (chamber).</param>
public sealed record HeaterSnapshot(string Heater, double Actual, double Target)
{
	public bool IsHotend => Heater.StartsWith('T');
}
