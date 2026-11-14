using Mediator;

using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Features.Files.Queries;

/// <summary>Extrusion paths of an uploaded file for the viewer; null when the file does not exist.</summary>
public sealed record GetGcodeToolpathQuery(string FileName) : IQuery<ToolpathResult?>;

/// <summary>The toolpath and the upload it was parsed from (for cache validation).</summary>
public sealed record ToolpathResult(GcodeFile File, GcodeToolpath Toolpath);
