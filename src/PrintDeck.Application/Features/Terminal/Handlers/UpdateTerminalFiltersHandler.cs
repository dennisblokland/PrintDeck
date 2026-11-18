using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Terminal.Commands;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Terminal;

namespace PrintDeck.Application.Features.Terminal.Handlers;

public sealed class UpdateTerminalFiltersHandler(ISettingsStore settings, PrinterEventHub events)
	: ICommandHandler<UpdateTerminalFiltersCommand, Result>
{
	public async ValueTask<Result> Handle(UpdateTerminalFiltersCommand c, CancellationToken ct)
	{
		List<TerminalFilter> filters = c.Filters
			.Select(f => new TerminalFilter(f.Name?.Trim() ?? "", f.Pattern?.Trim() ?? "", f.Enabled))
			.ToList();

		if (filters.Any(f => f.Name.Length == 0))
			return Result.Failure("Every filter needs a name.");
		if (filters.Any(f => f.Name.Length > TerminalFilter.MaxNameLength))
			return Result.Failure($"Filter names can be at most {TerminalFilter.MaxNameLength} characters.");
		if (filters.DistinctBy(f => f.Name, StringComparer.OrdinalIgnoreCase).Count() != filters.Count)
			return Result.Failure("Filter names must be unique.");
		foreach (TerminalFilter f in filters)
		{
			if (f.Pattern.Length == 0) return Result.Failure($"{f.Name}: enter a pattern.");
			if (f.Pattern.Length > TerminalFilter.MaxPatternLength)
				return Result.Failure($"{f.Name}: the pattern can be at most {TerminalFilter.MaxPatternLength} characters.");
			if (!TerminalFilter.IsValidPattern(f.Pattern)) return Result.Failure($"{f.Name}: the pattern is not a valid regular expression.");
		}

		await settings.UpdateAsync(s => s with { TerminalFilters = filters }, ct);
		events.Publish(PrinterChange.Settings);
		return Result.Success();
	}
}
