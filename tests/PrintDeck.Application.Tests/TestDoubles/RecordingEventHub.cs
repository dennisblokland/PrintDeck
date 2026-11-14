using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;

namespace PrintDeck.Application.Tests.TestDoubles;

internal static class RecordingEventHub
{
	/// <summary>A real hub plus the list of changes published on it.</summary>
	public static (PrinterEventHub Hub, List<PrinterChange> Published) Create()
	{
		PrinterEventHub hub = new();
		List<PrinterChange> published = [];
		hub.Changed += c => { lock (published) published.Add(c); };
		return (hub, published);
	}
}
