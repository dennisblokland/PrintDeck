using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Application.Files;
using PrintDeck.Domain.Files;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Files.Handlers;

public sealed class CheckGcodeFileHandler(IGcodeFileStore files, ISettingsStore settings)
	: IQueryHandler<CheckGcodeFileQuery, IReadOnlyList<GcodeProblem>?>
{
	public async ValueTask<IReadOnlyList<GcodeProblem>?> Handle(CheckGcodeFileQuery query, CancellationToken ct)
	{
		if (await files.GetAsync(query.FileName, ct) is not { } file) return null;
		HostSettings s = await settings.GetAsync(ct);
		await using Stream stream = files.OpenRead(file.Name);
		return await GcodeFileChecker.CheckAsync(stream, s.ActiveProfile, ct);
	}
}
