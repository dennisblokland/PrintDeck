using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using PrintDeck.Plugins;

namespace PrintDeck.Application.Plugins;

/// <summary>Reads and writes a module's settings as JSON, and keeps its <see cref="SecretAttribute"/> properties out of pages.</summary>
public static class ModuleSettingsJson
{
	public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

	/// <summary>The secret properties of <paramref name="settingsType"/>: JSON name → property name.</summary>
	public static IReadOnlyDictionary<string, string> Secrets(Type? settingsType) =>
		settingsType is null
			? new Dictionary<string, string>()
			: settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.IsDefined(typeof(SecretAttribute)))
				.ToDictionary(JsonName, p => p.Name);

	private static string JsonName(PropertyInfo p) =>
		p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? Options.PropertyNamingPolicy?.ConvertName(p.Name) ?? p.Name;

	/// <summary>A copy of <paramref name="settings"/> with every secret removed.</summary>
	public static JsonObject WithoutSecrets(JsonObject? settings, Type? settingsType)
	{
		JsonObject copy = Copy(settings);
		foreach (string name in Secrets(settingsType).Keys) copy.Remove(name);
		return copy;
	}

	/// <summary>Property names of the secrets that have a saved value.</summary>
	public static IReadOnlySet<string> SavedSecrets(JsonObject? settings, Type? settingsType) =>
		Secrets(settingsType)
			.Where(s => settings?[s.Key] is JsonNode value && !IsEmpty(value))
			.Select(s => s.Value)
			.ToHashSet(StringComparer.Ordinal);

	/// <summary><paramref name="incoming"/>, with each secret it leaves empty taken from <paramref name="saved"/>.</summary>
	public static JsonObject KeepSavedSecrets(JsonObject incoming, JsonObject? saved, Type? settingsType)
	{
		JsonObject merged = Copy(incoming);
		foreach (string name in Secrets(settingsType).Keys)
		{
			if (merged[name] is { } value && !IsEmpty(value)) continue;
			if (saved?[name] is { } kept) merged[name] = kept.DeepClone();
		}
		return merged;
	}

	public static JsonObject Copy(JsonObject? settings) => settings?.DeepClone().AsObject() ?? [];

	public static T Read<T>(JsonObject? settings) where T : class, new() =>
		settings is null ? new T() : settings.Deserialize<T>(Options) ?? new T();

	public static JsonObject Write<T>(T settings) where T : class =>
		JsonSerializer.SerializeToNode(settings, settings.GetType(), Options)?.AsObject() ?? [];

	private static bool IsEmpty(JsonNode value) =>
		value.GetValueKind() == JsonValueKind.Null
		|| value.GetValueKind() == JsonValueKind.String && value.GetValue<string>().Length == 0;
}
