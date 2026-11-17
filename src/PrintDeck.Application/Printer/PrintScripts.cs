using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Printer;

/// <summary>The G-code scripts for one print, with snippets filled in. Taken when the print starts.</summary>
public sealed record PrintScripts(
	IReadOnlyList<string> BeforePrint,
	IReadOnlyList<string> AfterPrint,
	IReadOnlyList<string> Cancel,
	IReadOnlyList<string> Pause,
	IReadOnlyList<string> Resume)
{
	public static readonly PrintScripts None = new([], [], [], [], []);

	/// <summary>
	/// The scripts in <paramref name="settings"/>. Saving checks every script, so a script that does not expand
	/// (a hand-edited settings file) is left out rather than sent half-done.
	/// </summary>
	public static PrintScripts From(HostSettings settings)
	{
		IReadOnlyList<string> Expand(string script) =>
			GcodeScript.Expand(script, settings.Snippets) is { IsSuccess: true } r ? r.Value! : [];

		return new PrintScripts(
			Expand(settings.BeforePrintGcode),
			Expand(settings.AfterPrintGcode),
			Expand(settings.CancelGcode),
			Expand(settings.PauseGcode),
			Expand(settings.ResumeGcode));
	}
}
