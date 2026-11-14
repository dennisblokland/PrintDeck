using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;

namespace PrintDeck.Application.Tests.Abstractions;

public class PrinterEventHubTests
{
	[Fact]
	public void Publishing_without_subscribers_is_fine()
	{
		new PrinterEventHub().Publish(PrinterChange.All);
	}

	[Fact]
	public void Delivers_the_change_to_every_subscriber()
	{
		PrinterEventHub hub = new();
		List<PrinterChange> a = [], b = [];
		hub.Changed += a.Add;
		hub.Changed += b.Add;

		hub.Publish(PrinterChange.State | PrinterChange.Job);

		Assert.Equal([PrinterChange.State | PrinterChange.Job], a);
		Assert.Equal(a, b);
	}

	[Fact]
	public void A_throwing_subscriber_does_not_stop_the_others()
	{
		PrinterEventHub hub = new();
		List<PrinterChange> received = [];
		hub.Changed += _ => throw new InvalidOperationException("broken component");
		hub.Changed += received.Add;

		hub.Publish(PrinterChange.Temperature);

		Assert.Equal([PrinterChange.Temperature], received);
	}

	[Fact]
	public void Unsubscribed_handlers_are_not_called()
	{
		PrinterEventHub hub = new();
		List<PrinterChange> received = [];
		Action<PrinterChange> handler = received.Add;
		hub.Changed += handler;
		hub.Changed -= handler;

		hub.Publish(PrinterChange.Files);

		Assert.Empty(received);
	}
}
