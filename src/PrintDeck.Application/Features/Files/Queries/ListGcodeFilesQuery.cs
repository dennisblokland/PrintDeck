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
using PrintDeck.Domain.Files.Enums;

namespace PrintDeck.Application.Features.Files.Queries;

/// <summary>Stored files, searched, sorted and filtered (see <see cref="FileListView"/>). The defaults list everything, newest first.</summary>
public sealed record ListGcodeFilesQuery(string? Search = null, FileSort Sort = FileSort.Uploaded, bool HidePrinted = false)
	: IQuery<IReadOnlyList<GcodeFile>>;
