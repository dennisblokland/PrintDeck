using System.Globalization;

using PrintDeck.Web.Services.Enums;

namespace PrintDeck.Web.Services;

public static class Format
{
	public static string Duration(TimeSpan? t)
	{
		if (t is not { } v) return "—";
		if (v.TotalSeconds < 60) return $"{Math.Max(0, v.Seconds)}s";
		if (v.TotalHours < 1) return $"{v.Minutes}m {v.Seconds:00}s";
		if (v.TotalDays < 1) return $"{(int) v.TotalHours}h {v.Minutes:00}m";
		return $"{(int) v.TotalDays}d {v.Hours}h";
	}

	public static string Bytes(long b) => b switch
	{
		< 1024 => $"{b} B",
		< 1024 * 1024 => $"{b / 1024.0:0.#} KB",
		< 1024L * 1024 * 1024 => $"{b / 1024.0 / 1024:0.#} MB",
		_ => $"{b / 1024.0 / 1024 / 1024:0.##} GB"
	};

	public static string Filament(double? mm) => mm is { } v ? (v >= 1000 ? $"{v / 1000:0.##} m" : $"{v:0} mm") : "—";

	public static string Temp(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

	public static string Ago(DateTimeOffset at, DateTimeOffset now)
	{
		TimeSpan d = now - at;
		return d.TotalMinutes < 1 ? "just now"
			: d.TotalHours < 1 ? $"{(int) d.TotalMinutes} min ago"
			: d.TotalDays < 1 ? $"{(int) d.TotalHours} h ago"
			: d.TotalDays < 7 ? $"{(int) d.TotalDays} d ago"
			: at.LocalDateTime.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
	}
}
