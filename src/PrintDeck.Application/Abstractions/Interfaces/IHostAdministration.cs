namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>
/// Marks a message about the host machine (restart, reboot, its logs): only a signed-in admin may send it,
/// not the slicer API key.
/// </summary>
public interface IHostAdministration;
