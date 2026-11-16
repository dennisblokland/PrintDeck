using System.Globalization;
using System.Text;

using PrintDeck.Domain.Jobs.Enums;
using PrintDeck.Domain.Protocol;

namespace PrintDeck.Domain.Jobs;

/// <summary>
/// Decides what PrintDeck shows on the printer's own screen during a streamed print:
/// <c>M73 P&lt;percent&gt; R&lt;minutes left&gt;</c> for the progress bar and <c>M117</c> status messages
/// (file name, layer, paused, done). Stays out of the way of files that bring their own M73 (PrusaSlicer, OrcaSlicer).
/// </summary>
public sealed class PrinterScreenReporter(string fileName, int? layerCount)
{
	/// <summary>Character LCDs (Prusa MK3, RepRapDiscount) show 20 columns.</summary>
	public const int MessageLength = 20;

	private static readonly TimeSpan _minInterval = TimeSpan.FromSeconds(5);

	private int _lastPercent = -1;
	private int? _lastMinutes;
	private DateTimeOffset _lastSent = DateTimeOffset.MinValue;
	private bool _explicitLayers;

	/// <summary>True once the file turned out to send its own M73; PrintDeck then sends none.</summary>
	public bool FileReportsProgress { get; private set; }

	/// <summary>Current layer (1-based), once the file has marked one.</summary>
	public int Layer { get; private set; }

	public IReadOnlyList<string> Start() => ["M73 P0", Message(Path.GetFileNameWithoutExtension(fileName))];

	/// <summary>Looks at each raw line streamed to the printer; returns an M117 when a new layer starts.</summary>
	public string? OnLine(string raw)
	{
		string line = raw.Trim();
		if (line.StartsWith(';'))
		{
			string comment = line[1..].TrimStart();
			int? layer = null;
			// Cura and OrcaSlicer number layers from 0 ("LAYER:0"); PrusaSlicer only marks the change.
			if (comment.StartsWith("LAYER:", StringComparison.Ordinal)
				&& int.TryParse(comment.AsSpan(6), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
			{
				_explicitLayers = true;
				layer = n + 1;
			}
			else if (!_explicitLayers && comment.StartsWith("LAYER_CHANGE", StringComparison.Ordinal))
				layer = Layer + 1;

			if (layer is not { } l || l == Layer) return null;
			Layer = l;
			return Message(layerCount is { } total && total >= l ? $"Layer {l}/{total}" : $"Layer {l}");
		}

		if (!FileReportsProgress && GcodeLine.Clean(raw) is { } command && GcodeLine.CommandOf(command) == "M73")
			FileReportsProgress = true;
		return null;
	}

	/// <summary>An M73 when the percentage or the minutes left changed, at most every few seconds.</summary>
	public string? OnProgress(double progress, TimeSpan? remaining, DateTimeOffset now)
	{
		if (FileReportsProgress) return null;

		int percent = (int) Math.Floor(Math.Clamp(progress, 0, 1) * 100);
		int? minutes = remaining is { } r ? (int) Math.Ceiling(Math.Max(0, r.TotalMinutes)) : null;
		if (percent == _lastPercent && minutes == _lastMinutes) return null;
		if (now - _lastSent < _minInterval) return null;

		_lastPercent = percent;
		_lastMinutes = minutes;
		_lastSent = now;
		return minutes is { } m ? $"M73 P{percent} R{m}" : $"M73 P{percent}";
	}

	public IReadOnlyList<string> Paused() => [Message("Paused")];

	public IReadOnlyList<string> Resumed() =>
		[Message(Layer > 0 ? (layerCount is { } total && total >= Layer ? $"Layer {Layer}/{total}" : $"Layer {Layer}") : "Printing")];

	public IReadOnlyList<string> Finish(JobStatus status) => status switch
	{
		JobStatus.Completed => FileReportsProgress ? [Message("Print done")] : ["M73 P100 R0", Message("Print done")],
		JobStatus.Cancelled => [Message("Print cancelled")],
		_ => []
	};

	/// <summary>"M117 text": printable ASCII only (no ';' that would start a comment), cut to the screen width.</summary>
	public static string Message(string text)
	{
		StringBuilder clean = new(MessageLength);
		foreach (char c in text)
		{
			if (clean.Length == MessageLength) break;
			if (c is >= ' ' and <= '~' and not ';' and not '*') clean.Append(c);
		}
		return ("M117 " + clean.ToString().Trim()).TrimEnd();
	}
}
