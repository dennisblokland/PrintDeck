using PrintDeck.Plugins.Enums;

namespace PrintDeck.Plugins;

/// <param name="Job">The current or last print; null when there was none since connecting.</param>
public sealed record PrinterSnapshot(PrinterActivity Activity, JobSnapshot? Job, IReadOnlyList<HeaterSnapshot> Heaters);
