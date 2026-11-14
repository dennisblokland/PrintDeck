using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Tests.TestDoubles;

internal sealed class FakeSettingsStore(PrinterProfile? profile = null) : ISettingsStore
{
	public PrinterProfile Profile { get; private set; } = profile ?? new PrinterProfile { ApiKey = "KEY" };
	public int Saves { get; private set; }

	public Task<PrinterProfile> GetAsync(CancellationToken ct) => Task.FromResult(Profile);

	public Task SaveAsync(PrinterProfile profile, CancellationToken ct)
	{
		Profile = profile;
		Saves++;
		return Task.CompletedTask;
	}
}
