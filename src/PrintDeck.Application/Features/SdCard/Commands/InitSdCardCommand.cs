using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.SdCard.Commands;

public sealed record InitSdCardCommand : ICommand<Result>;
