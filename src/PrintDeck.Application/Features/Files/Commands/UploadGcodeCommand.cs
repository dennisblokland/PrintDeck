using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Files.Handlers;
using PrintDeck.Application.Features.Files.Queries;
using PrintDeck.Application.Files;
using PrintDeck.Application.Printer;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Commands;

/// <param name="Folder">Where to put it, e.g. "parts/brackets"; created when missing. Null or "" for the top level.</param>
public sealed record UploadGcodeCommand(string FileName, Stream Content, string? Folder = null) : ICommand<Result<GcodeFile>>;
