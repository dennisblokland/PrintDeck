using System.Globalization;

using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Temperatures.Handlers;
using PrintDeck.Application.Features.Temperatures.Queries;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Temperatures;

namespace PrintDeck.Application.Features.Temperatures.Commands;

public sealed record SetTemperatureCommand(string Heater, double Target) : ICommand<Result>;
