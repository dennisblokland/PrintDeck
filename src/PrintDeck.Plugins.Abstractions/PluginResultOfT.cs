namespace PrintDeck.Plugins;

public sealed record PluginResult<T>(T? Value, bool IsSuccess, string? Error) : PluginResult(IsSuccess, Error);
