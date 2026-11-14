using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Abstractions.Interfaces;

public interface ISettingsStore
{
	Task<HostSettings> GetAsync(CancellationToken ct);

	/// <summary>Applies <paramref name="change"/> to the latest settings and saves the result, one change at a time.</summary>
	Task<HostSettings> UpdateAsync(Func<HostSettings, HostSettings> change, CancellationToken ct);
}
