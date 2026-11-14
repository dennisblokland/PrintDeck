using Mediator;

using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Settings.Commands;

public sealed record DeletePrinterProfileCommand(string Id) : ICommand<Result>;
