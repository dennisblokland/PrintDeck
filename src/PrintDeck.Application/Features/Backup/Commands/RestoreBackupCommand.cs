using Mediator;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Backup;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Features.Backup.Commands;

/// <summary>Restores settings, printer profiles and files from a backup archive. Admin accounts only, never the slicer API key.</summary>
public sealed record RestoreBackupCommand(Stream Archive) : ICommand<Result<BackupRestore>>, IHostAdministration;
