namespace PrintDeck.Plugins;

/// <summary>The outcome of an action. <see cref="Error"/> is a short sentence to show the user.</summary>
public record PluginResult(bool IsSuccess, string? Error)
{
	public static PluginResult Success() => new(true, null);
	public static PluginResult Failure(string error) => new(false, error);
	public static PluginResult<T> Success<T>(T value) => new(value, true, null);
	public static PluginResult<T> Failure<T>(string error) => new(default, false, error);
}
