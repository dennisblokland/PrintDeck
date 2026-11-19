using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Timelapse.Commands;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Timelapse;

namespace PrintDeck.Application.Features.Timelapse.Handlers;

public sealed class DeleteTimelapseHandler(ITimelapseStore store, PrinterEventHub events) : ICommandHandler<DeleteTimelapseCommand, Result>
{
	public const string NotFound = "That timelapse doesn't exist.";

	public ValueTask<Result> Handle(DeleteTimelapseCommand c, CancellationToken ct)
	{
		if (!TimelapseName.IsValid(c.Name) || !store.Delete(c.Name)) return ValueTask.FromResult(Result.Failure(NotFound));
		events.Publish(PrinterChange.Timelapse);
		return ValueTask.FromResult(Result.Success());
	}
}
