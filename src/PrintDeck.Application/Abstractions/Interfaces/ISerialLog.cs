using PrintDeck.Domain.Terminal;

namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>Optional full record of the serial traffic, written to a file while <see cref="Enabled"/>.</summary>
public interface ISerialLog
{
	bool Enabled { get; }

	void SetEnabled(bool enabled);

	/// <summary>Records one terminal line; ignored while disabled.</summary>
	void Write(TerminalEntry entry);
}
