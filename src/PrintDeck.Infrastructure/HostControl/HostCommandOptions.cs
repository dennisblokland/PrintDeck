namespace PrintDeck.Infrastructure.HostControl;

/// <summary>
/// Shell commands behind the System menu, from the <c>System</c> section of appsettings.json (or environment variables
/// such as <c>System__RebootCommand</c>). They are deliberately not editable in the web UI, so an admin account
/// cannot be turned into arbitrary command execution. Empty means the action is hidden.
/// </summary>
public sealed class HostCommandOptions
{
	public const string Section = "System";

	/// <summary>E.g. <c>sudo systemctl restart printdeck</c>.</summary>
	public string? RestartCommand { get; set; }

	/// <summary>E.g. <c>sudo systemctl reboot</c>.</summary>
	public string? RebootCommand { get; set; }

	/// <summary>E.g. <c>sudo systemctl poweroff</c>.</summary>
	public string? ShutdownCommand { get; set; }
}
