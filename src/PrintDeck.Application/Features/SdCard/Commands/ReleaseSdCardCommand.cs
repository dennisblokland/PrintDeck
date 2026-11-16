using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.SdCard.Commands;

public sealed record ReleaseSdCardCommand : ICommand<Result>;
