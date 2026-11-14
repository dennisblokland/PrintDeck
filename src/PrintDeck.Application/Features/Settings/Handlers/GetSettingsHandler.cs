using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Settings.Queries;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Handlers;

public sealed class GetSettingsHandler(ISettingsStore settings) : IQueryHandler<GetSettingsQuery, HostSettings>
{
	public ValueTask<HostSettings> Handle(GetSettingsQuery q, CancellationToken ct) => new(settings.GetAsync(ct));
}
