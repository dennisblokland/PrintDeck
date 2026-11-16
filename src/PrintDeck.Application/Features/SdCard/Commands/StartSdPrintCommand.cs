using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.SdCard.Commands;

/// <summary>Prints a file from the printer's SD card; <paramref name="Path"/> is its 8.3 path.</summary>
public sealed record StartSdPrintCommand(string Path) : ICommand<Result>;
