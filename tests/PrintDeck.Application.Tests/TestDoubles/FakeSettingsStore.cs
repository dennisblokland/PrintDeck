using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Tests.TestDoubles;

internal sealed class FakeSettingsStore(HostSettings? settings = null) : ISettingsStore
{
	/// <summary>Settings whose only (and active) profile is <paramref name="profile"/>.</summary>
	public FakeSettingsStore(PrinterProfile profile) : this(new HostSettings { ApiKey = "KEY", Profiles = [profile], ActiveProfileId = profile.Id }) { }

	public HostSettings Settings { get; private set; } = settings ?? new HostSettings { ApiKey = "KEY" };
	public int Saves { get; private set; }

	public Task<HostSettings> GetAsync(CancellationToken ct) => Task.FromResult(Settings);

	public Task<HostSettings> UpdateAsync(Func<HostSettings, HostSettings> change, CancellationToken ct)
	{
		Settings = change(Settings).Normalized();
		Saves++;
		return Task.FromResult(Settings);
	}
}
