using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Jobs.Handlers;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Jobs.Commands;

/// <summary>
/// Streams a stored file to the printer. Unless <paramref name="IgnoreProblems"/> is set, a file with a known
/// error (see <c>GcodeProblemCheck</c>) is refused; warnings never block.
/// </summary>
public sealed record StartPrintCommand(string FileName, bool IgnoreProblems = false) : ICommand<Result>;
