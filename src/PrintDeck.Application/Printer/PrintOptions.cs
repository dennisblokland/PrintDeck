namespace PrintDeck.Application.Printer;

/// <summary>How a streamed print behaves beyond sending its lines.</summary>
/// <param name="ShowProgressOnPrinter">Send M73 progress and M117 messages to the printer's screen.</param>
/// <param name="LayerCount">Total layers from the file analysis, for "Layer 3/80".</param>
/// <param name="Scripts">G-code sent before and after the print and around a pause.</param>
public sealed record PrintOptions(bool ShowProgressOnPrinter = false, int? LayerCount = null, PrintScripts? Scripts = null);
