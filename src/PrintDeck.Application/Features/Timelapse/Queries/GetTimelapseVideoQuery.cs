using Mediator;

namespace PrintDeck.Application.Features.Timelapse.Queries;

/// <summary>The content of a rendered timelapse; null when there is no such video.</summary>
public sealed record GetTimelapseVideoQuery(string Name) : IQuery<Stream?>;
