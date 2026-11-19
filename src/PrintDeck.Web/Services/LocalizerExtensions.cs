using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using PrintDeck.Domain.Jobs.Enums;

namespace PrintDeck.Web.Services;

public static class LocalizerExtensions
{
	/// <summary>
	/// A resource whose text holds trusted markup (<c>&lt;code&gt;</c>, <c>&lt;strong&gt;</c>, links), so a translation keeps
	/// the sentence whole around it. Arguments are HTML-encoded before they go in; keys used this way end in <c>Html</c>.
	/// </summary>
	public static MarkupString Html(this IStringLocalizer localizer, string key, params object?[] args)
	{
		string text = localizer[key].Value;
		if (args.Length == 0) return new MarkupString(text);
		object?[] encoded = args.Select(a => (object?) HtmlEncoder.Default.Encode(Convert.ToString(a, System.Globalization.CultureInfo.CurrentCulture) ?? "")).ToArray();
		return new MarkupString(string.Format(System.Globalization.CultureInfo.CurrentCulture, text, encoded));
	}

	/// <summary>A print's status as the UI names it (chips on the job card, files and history).</summary>
	public static string JobStatusName(this IStringLocalizer<SharedText> shared, JobStatus status) => status switch
	{
		JobStatus.Printing => shared["JobStatusPrinting"],
		JobStatus.Paused => shared["JobStatusPaused"],
		JobStatus.Cancelled => shared["JobStatusCancelled"],
		JobStatus.Completed => shared["JobStatusCompleted"],
		_ => shared["JobStatusFailed"]
	};
}
