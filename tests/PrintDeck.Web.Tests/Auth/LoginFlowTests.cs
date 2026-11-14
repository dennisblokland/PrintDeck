using System.Net;

namespace PrintDeck.Web.Tests.Auth;

/// <summary>First-run setup, login, lockout and logout against the real app (fresh data directory per test).</summary>
public sealed class LoginFlowTests : IAsyncDisposable
{
	private readonly PrintDeckWebFactory _app = new();
	private readonly BrowserSession _browser;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public LoginFlowTests() => _browser = new BrowserSession(_app);

	public ValueTask DisposeAsync() => _app.DisposeAsync();

	private static string Location(HttpResponseMessage r) => r.Headers.Location?.OriginalString ?? "";

	[Fact]
	public async Task A_fresh_install_sends_browsers_to_setup()
	{
		HttpResponseMessage r = await _browser.Open("/");

		Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
		Assert.EndsWith("/setup", Location(r));
	}

	[Fact]
	public async Task Setup_creates_the_admin_signs_in_and_then_closes()
	{
		HttpResponseMessage created = await _browser.SetUp();

		Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await _browser.Open("/")).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await _browser.Open("/users")).StatusCode);

		BrowserSession stranger = new(_app);
		HttpResponseMessage again = await stranger.Open("/setup");
		Assert.Contains("/login", Location(again));
		Assert.Contains("/login", Location(await stranger.Open("/")));
	}

	[Fact]
	public async Task Setup_rejects_mismatched_passwords()
	{
		HttpResponseMessage r = await _browser.Submit("/setup", new Dictionary<string, string>
		{
			["Input.UserName"] = "ada", ["Input.Password"] = "correct-horse", ["Input.ConfirmPassword"] = "battery-staple"
		});

		Assert.Equal(HttpStatusCode.OK, r.StatusCode);
		Assert.Contains("The passwords don&#x27;t match.", await r.Content.ReadAsStringAsync(Ct));
		Assert.Contains("/setup", Location(await _browser.Open("/")));
	}

	[Fact]
	public async Task Wrong_passwords_are_refused_and_lock_the_account()
	{
		await _browser.SetUp("ada", "correct-horse");
		BrowserSession other = new(_app);

		HttpResponseMessage wrong = await other.LogIn("ada", "wrong-password");
		Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
		Assert.Contains("Wrong username or password.", await wrong.Content.ReadAsStringAsync(Ct));

		for (int i = 0; i < 4; i++) await other.LogIn("ada", "wrong-password");
		HttpResponseMessage locked = await other.LogIn("ada", "correct-horse");
		Assert.Contains("Too many wrong passwords.", await locked.Content.ReadAsStringAsync(Ct));
		Assert.Contains("/login", Location(await other.Open("/")));
	}

	[Fact]
	public async Task Login_returns_to_the_page_you_asked_for_but_never_off_site()
	{
		await _browser.SetUp("ada", "correct-horse");

		HttpResponseMessage local = await new BrowserSession(_app).LogIn("ada", "correct-horse", "/login?returnUrl=%2Ffiles");
		Assert.Equal(HttpStatusCode.Redirect, local.StatusCode);
		Assert.EndsWith("/files", Location(local));

		HttpResponseMessage offsite = await new BrowserSession(_app).LogIn("ada", "correct-horse", "/login?returnUrl=%2F%2Fevil.example%2F");
		Assert.Equal(HttpStatusCode.Redirect, offsite.StatusCode);
		Assert.DoesNotContain("evil.example", Location(offsite));
	}

	[Fact]
	public async Task Logout_needs_the_antiforgery_token_and_then_signs_out()
	{
		await _browser.SetUp();

		HttpResponseMessage forged = await _browser.Client.PostAsync("/account/logout", new FormUrlEncodedContent([]), Ct);
		Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await _browser.Open("/")).StatusCode);

		HttpResponseMessage logout = await _browser.Submit("/account", new Dictionary<string, string>(), action: "/account/logout");
		Assert.EndsWith("/login", Location(logout));
		Assert.Contains("/login", Location(await _browser.Open("/")));
	}

	[Fact]
	public async Task The_slicer_api_key_still_works_without_a_login()
	{
		await _browser.SetUp();

		HttpResponseMessage r = await _app.CreateClient().GetAsync($"/api/version?apikey={await _app.ApiKeyAsync()}", Ct);

		Assert.Equal(HttpStatusCode.OK, r.StatusCode);
	}
}
