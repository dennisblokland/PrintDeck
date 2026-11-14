using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Files.Commands;
using PrintDeck.Application.Features.Files.Handlers;
using PrintDeck.Application.Files;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Queries;

public sealed record ListGcodeFilesQuery : IQuery<IReadOnlyList<GcodeFile>>;
