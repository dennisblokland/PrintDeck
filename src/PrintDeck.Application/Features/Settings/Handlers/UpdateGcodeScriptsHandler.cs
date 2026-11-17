using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Handlers;

public sealed class UpdateGcodeScriptsHandler(ISettingsStore settings, PrinterEventHub events)
	: ICommandHandler<UpdateGcodeScriptsCommand, Result>
{
	public async ValueTask<Result> Handle(UpdateGcodeScriptsCommand c, CancellationToken ct)
	{
		List<GcodeSnippet> snippets = c.Snippets.Select(s => new GcodeSnippet(s.Name?.Trim() ?? "", Text(s.Gcode))).ToList();
		if (snippets.Any(s => !GcodeScript.IsValidSnippetName(s.Name)))
			return Result.Failure("Snippet names need letters, digits, - or _ only.");
		if (snippets.DistinctBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Count() != snippets.Count)
			return Result.Failure("Snippet names must be unique.");

		(string Label, string Script)[] scripts =
		[
			("Before print", Text(c.BeforePrint)),
			("After print", Text(c.AfterPrint)),
			("After cancel", Text(c.Cancel)),
			("On pause", Text(c.Pause)),
			("On resume", Text(c.Resume)),
			.. snippets.Select(s => ($"Snippet {s.Name}", s.Gcode))
		];
		foreach ((string label, string script) in scripts)
			if (GcodeScript.Expand(script, snippets) is { IsSuccess: false } failed)
				return Result.Failure($"{label}: {failed.Error}");

		await settings.UpdateAsync(s => s with
		{
			BeforePrintGcode = scripts[0].Script,
			AfterPrintGcode = scripts[1].Script,
			CancelGcode = scripts[2].Script,
			PauseGcode = scripts[3].Script,
			ResumeGcode = scripts[4].Script,
			Snippets = snippets
		}, ct);
		events.Publish(PrinterChange.Settings);
		return Result.Success();
	}

	private static string Text(string? script) => (script ?? "").Replace("\r", "").Trim('\n');
}
