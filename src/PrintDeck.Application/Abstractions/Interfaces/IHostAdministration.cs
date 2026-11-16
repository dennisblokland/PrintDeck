namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>Marks a message that acts on the host machine: only a signed-in admin may send it, not the slicer API key.</summary>
public interface IHostAdministration;
