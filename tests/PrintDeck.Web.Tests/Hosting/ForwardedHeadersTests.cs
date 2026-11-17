using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Web.Tests.Auth;

namespace PrintDeck.Web.Tests.Hosting;

/// <summary>
/// PrintDeck behind a reverse proxy: every request arrives from <see cref="Proxy"/>, and the proxy says who the client
/// is in <c>X-Forwarded-For</c>. Only proxies listed under <c>ReverseProxy</c> are believed.
/// </summary>
public sealed class ForwardedHeadersTests : IAsyncDisposable
{
	private static readonly IPAddress Proxy = IPAddress.Parse("10.9.9.9");
	private const string Guesser = "203.0.113.1";
	private const string Owner = "203.0.113.2";
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private readonly ConnectionFromProxy _connection = new(Proxy);
	private PrintDeckWebFactory? _app;

	public ValueTask DisposeAsync() => _app?.DisposeAsync() ?? ValueTask.CompletedTask;

	private PrintDeckWebFactory Start(params (string Key, string Value)[] settings) =>
		_app = new PrintDeckWebFactory
		{
			ConfigureHost = b =>
			{
				foreach ((string key, string value) in settings) b.UseSetting(key, value);
				b.ConfigureServices(s => s.AddSingleton<IStartupFilter>(_connection));
			}
		};

	private static BrowserSession From(PrintDeckWebFactory app, string client)
	{
		BrowserSession browser = new(app);
		browser.Client.DefaultRequestHeaders.Add("X-Forwarded-For", client);
		return browser;
	}

	/// <summary>Ten wrong guesses from <see cref="Guesser"/>, then the owner signs in from another address.</summary>
	private static async Task<(HttpResponseMessage Guesser, HttpResponseMessage Owner)> GuessThenSignIn(PrintDeckWebFactory app)
	{
		await new BrowserSession(app).SetUp("ada", "correct-horse");
		BrowserSession guesser = From(app, Guesser);
		for (int i = 0; i < 10; i++) await guesser.LogIn($"user{i}", "guess");
		return (await guesser.LogIn("ada", "correct-horse"), await From(app, Owner).LogIn("ada", "correct-horse"));
	}

	[Theory]
	[InlineData("ReverseProxy:KnownProxies:0", "10.9.9.9")]
	[InlineData("ReverseProxy:KnownNetworks:0", "10.0.0.0/8")]
	public async Task Behind_a_known_proxy_each_forwarded_client_has_its_own_login_limit(string key, string value)
	{
		(HttpResponseMessage guesser, HttpResponseMessage owner) = await GuessThenSignIn(Start((key, value)));

		Assert.Equal(HttpStatusCode.TooManyRequests, guesser.StatusCode);
		Assert.Equal(HttpStatusCode.Redirect, owner.StatusCode);
		Assert.Equal(IPAddress.Parse(Owner), _connection.LastClient);
	}

	[Fact]
	public async Task Without_configured_proxies_the_header_is_ignored()
	{
		(HttpResponseMessage guesser, HttpResponseMessage owner) = await GuessThenSignIn(Start());

		Assert.Equal(HttpStatusCode.TooManyRequests, guesser.StatusCode);
		Assert.Equal(HttpStatusCode.TooManyRequests, owner.StatusCode);
		Assert.Equal(Proxy, _connection.LastClient);
	}

	[Fact]
	public async Task Headers_from_an_unlisted_proxy_are_ignored()
	{
		(HttpResponseMessage _, HttpResponseMessage owner) = await GuessThenSignIn(Start(("ReverseProxy:KnownProxies:0", "10.1.1.1")));

		Assert.Equal(HttpStatusCode.TooManyRequests, owner.StatusCode);
		Assert.Equal(Proxy, _connection.LastClient);
	}

	[Fact]
	public async Task A_known_proxy_passes_on_https_and_only_its_own_hop_counts()
	{
		PrintDeckWebFactory app = Start(("ReverseProxy:KnownProxies:0", "10.9.9.9"));
		HttpRequestMessage request = new(HttpMethod.Get, "/login");
		// The client made up the first address; the proxy appended the one it saw.
		request.Headers.Add("X-Forwarded-For", $"198.51.100.7, {Owner}");
		request.Headers.Add("X-Forwarded-Proto", "https");

		HttpResponseMessage response = await app.CreateClient().SendAsync(request, Ct);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(IPAddress.Parse(Owner), _connection.LastClient);
		Assert.Equal("https", _connection.LastScheme);
	}

	/// <summary>
	/// TestServer requests have no remote address; this gives them the proxy's, and notes what the app finally saw
	/// (the forwarded-headers middleware rewrites the request in place).
	/// </summary>
	private sealed class ConnectionFromProxy(IPAddress proxy) : IStartupFilter
	{
		public IPAddress? LastClient { get; private set; }
		public string? LastScheme { get; private set; }

		public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
		{
			app.Use(async (context, inner) =>
			{
				context.Connection.RemoteIpAddress = proxy;
				try { await inner(context); }
				finally
				{
					LastClient = context.Connection.RemoteIpAddress;
					LastScheme = context.Request.Scheme;
				}
			});
			next(app);
		};
	}
}
