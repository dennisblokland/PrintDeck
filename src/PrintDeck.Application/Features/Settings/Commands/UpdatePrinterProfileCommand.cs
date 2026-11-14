using System.Security.Cryptography;

using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Settings.Handlers;
using PrintDeck.Application.Features.Settings.Queries;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Features.Settings.Commands;

public sealed record UpdatePrinterProfileCommand(PrinterProfile Profile) : ICommand<Result>;
