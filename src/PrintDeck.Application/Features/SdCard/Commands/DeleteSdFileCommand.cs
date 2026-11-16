using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.SdCard.Commands;

public sealed record DeleteSdFileCommand(string Path) : ICommand<Result>;
