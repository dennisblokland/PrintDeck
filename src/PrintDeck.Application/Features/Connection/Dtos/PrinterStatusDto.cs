using Mediator;

using PrintDeck.Application.Features.Connection.Commands;
using PrintDeck.Application.Features.Connection.Handlers;
using PrintDeck.Application.Features.Connection.Queries;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Jobs;
using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Printer;
using PrintDeck.Domain.Printer.Enums;
using PrintDeck.Domain.Temperatures;

namespace PrintDeck.Application.Features.Connection.Dtos;

public sealed record PrinterStatusDto(
	PrinterState State,
	string? Port,
	int BaudRate,
	string? Firmware,
	string? Error,
	JobDto? Job,
	IReadOnlyList<HeaterReading> Heaters);
