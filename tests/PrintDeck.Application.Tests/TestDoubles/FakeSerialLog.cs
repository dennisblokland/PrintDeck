using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Terminal;

namespace PrintDeck.Application.Tests.TestDoubles;

internal sealed class FakeSerialLog : ISerialLog
{
	private readonly List<TerminalEntry> _written = [];

	public bool Enabled { get; private set; }

	public IReadOnlyList<TerminalEntry> Written
	{
		get { lock (_written) return _written.ToList(); }
	}

	public void SetEnabled(bool enabled) => Enabled = enabled;

	public void Write(TerminalEntry entry)
	{
		if (Enabled) lock (_written) _written.Add(entry);
	}
}
