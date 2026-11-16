using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.SdCard.Commands;

/// <summary>Copies an uploaded G-code file onto the printer's SD card. Returns its 8.3 name there.</summary>
public sealed record CopyToSdCardCommand(string FileName) : ICommand<Result<string>>;
