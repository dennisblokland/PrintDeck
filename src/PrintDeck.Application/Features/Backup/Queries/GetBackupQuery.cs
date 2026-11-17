using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Features.Backup.Dtos;

namespace PrintDeck.Application.Features.Backup.Queries;

/// <summary>A backup download. It holds the API key, so admin accounts only, never the slicer API key.</summary>
public sealed record GetBackupQuery : IQuery<BackupDownload>, IHostAdministration;
