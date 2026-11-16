using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;

namespace PrintDeck.Application.Features.Logs.Queries;

/// <summary>Opens a log file for download; null when there is no such file. Admin accounts only.</summary>
public sealed record OpenLogFileQuery(string Name) : IQuery<Stream?>, IHostAdministration;
