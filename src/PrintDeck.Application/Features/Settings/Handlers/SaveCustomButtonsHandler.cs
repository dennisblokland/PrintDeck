using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Handlers;

public sealed class SaveCustomButtonsHandler(ISettingsStore settings, PrinterEventHub events)
	: ICommandHandler<SaveCustomButtonsCommand, Result>
{
	public async ValueTask<Result> Handle(SaveCustomButtonsCommand c, CancellationToken ct)
	{
		if (c.Buttons.Count > CustomButton.MaxButtons) return Result.Failure($"At most {CustomButton.MaxButtons} buttons.");

		List<CustomButton> buttons = c.Buttons.Select(b => b with
		{
			Label = b.Label?.Trim() ?? "",
			Gcode = (b.Gcode ?? "").Replace("\r", "").Trim('\n'),
			Icon = string.IsNullOrWhiteSpace(b.Icon) ? null : b.Icon.Trim()
		}).ToList();

		IReadOnlyList<GcodeSnippet> snippets = (await settings.GetAsync(ct)).Snippets;
		if (buttons.Select(b => b.Problem(snippets)).FirstOrDefault(p => p is not null) is { } problem) return Result.Failure(problem);

		await settings.UpdateAsync(s => s with { CustomButtons = buttons }, ct);
		events.Publish(PrinterChange.Settings);
		return Result.Success();
	}
}
