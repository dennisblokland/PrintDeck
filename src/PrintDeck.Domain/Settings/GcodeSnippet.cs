namespace PrintDeck.Domain.Settings;

/// <summary>A named piece of G-code that scripts include with <c>{% snippet 'name' %}</c>.</summary>
public sealed record GcodeSnippet(string Name, string Gcode);
