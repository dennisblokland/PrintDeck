using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Printer.Enums;

namespace PrintDeck.Domain.Tests.Printer;

public class PrinterStateExtensionsTests
{
	[Theory]
	[InlineData(PrinterState.Offline, false, false)]
	[InlineData(PrinterState.Connecting, false, false)]
	[InlineData(PrinterState.Operational, true, false)]
	[InlineData(PrinterState.Printing, true, true)]
	[InlineData(PrinterState.Paused, true, true)]
	[InlineData(PrinterState.Cancelling, true, true)]
	[InlineData(PrinterState.Transferring, true, true)]
	[InlineData(PrinterState.Error, false, false)]
	public void Classifies_states(PrinterState state, bool connected, bool busy)
	{
		Assert.Equal(connected, state.IsConnected());
		Assert.Equal(busy, state.IsBusy());
	}
}
