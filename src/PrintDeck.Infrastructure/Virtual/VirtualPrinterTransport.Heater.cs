namespace PrintDeck.Infrastructure.Virtual;

public sealed partial class VirtualPrinterTransport
{
	private sealed class Heater(string name, double ambient)
	{
		public string Name { get; } = name;
		public double Actual { get; set; } = ambient;
		public double Target { get; set; }
		public double Ambient { get; } = ambient;
		public string Report => FormattableString.Invariant($"{Name}:{Actual:0.00} /{Target:0.00}");
	}
}
