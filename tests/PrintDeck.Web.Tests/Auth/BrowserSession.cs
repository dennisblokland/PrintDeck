using System.Net;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Mvc.Testing;

namespace PrintDeck.Web.Tests.Auth;

/// <summary>
/// A cookie-keeping client that fills in server-rendered forms the way a browser does: it reads the
/// hidden fields (antiforgery token, Blazor's <c>_handler</c>) from the page and posts them back.
/// </summary>
internal sealed partial class BrowserSession(PrintDeckWebFactory app)
{
	public HttpClient Client { get; } = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public async Task<HttpResponseMessage> Open(string url)
	{
		HttpRequestMessage request = new(HttpMethod.Get, url);
		request.Headers.Accept.ParseAdd("text/html");
		return await Client.SendAsync(request, Ct);
	}

	/// <summary>Loads <paramref name="url"/>, takes the hidden fields of its first form, adds <paramref name="fields"/> and submits.</summary>
	public async Task<HttpResponseMessage> Submit(string url, IReadOnlyDictionary<string, string> fields, string? action = null)
	{
		HttpResponseMessage page = await Open(url);
		Assert.Equal(HttpStatusCode.OK, page.StatusCode);
		Dictionary<string, string> form = HiddenFields(await page.Content.ReadAsStringAsync(Ct));
		foreach ((string key, string value) in fields) form[key] = value;
		return await Client.PostAsync(action ?? url, new FormUrlEncodedContent(form), Ct);
	}

	public Task<HttpResponseMessage> SetUp(string userName = "ada", string password = "correct-horse") =>
		Submit("/setup", new Dictionary<string, string>
		{
			["Input.UserName"] = userName, ["Input.Password"] = password, ["Input.ConfirmPassword"] = password
		});

	public Task<HttpResponseMessage> LogIn(string userName, string password, string url = "/login") =>
		Submit(url, new Dictionary<string, string> { ["Input.UserName"] = userName, ["Input.Password"] = password });

	public static Dictionary<string, string> HiddenFields(string html) =>
		HiddenInput().Matches(html)
			.Select(m => (Name: Attr(m.Value, "name"), Value: Attr(m.Value, "value")))
			.Where(f => f.Name is not null)
			.GroupBy(f => f.Name!)
			.ToDictionary(g => g.Key, g => g.First().Value ?? "");

	private static string? Attr(string tag, string name) =>
		Regex.Match(tag, $"\\b{name}=\"([^\"]*)\"") is { Success: true } m ? WebUtility.HtmlDecode(m.Groups[1].Value) : null;

	[GeneratedRegex("<input[^>]*type=\"hidden\"[^>]*>")]
	private static partial Regex HiddenInput();
}
