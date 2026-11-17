using System.Net;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PrintDeck.Web.Auth;

namespace PrintDeck.Web.Tests.Auth;

public class LoginRateLimiterTests
{
	private static HttpContext From(string address) => new DefaultHttpContext { Connection = { RemoteIpAddress = IPAddress.Parse(address) } };

	private static LoginRateLimiter Limiter(int maxFailures) =>
		new(Options.Create(new LoginRateLimitOptions { MaxFailures = maxFailures, WindowMinutes = 15 }), TimeProvider.System, NullLogger<LoginRateLimiter>.Instance);

	[Fact]
	public void Counts_per_request_address()
	{
		LoginRateLimiter limiter = Limiter(2);
		limiter.RecordFailure(From("10.0.0.1"), "ada");
		limiter.RecordFailure(From("10.0.0.1"), "bob");

		Assert.NotNull(limiter.RetryAfter(From("10.0.0.1")));
		Assert.Null(limiter.RetryAfter(From("10.0.0.2")));
	}

	[Fact]
	public void Zero_switches_the_limit_off()
	{
		LoginRateLimiter limiter = Limiter(0);
		for (int i = 0; i < 50; i++) limiter.RecordFailure(From("10.0.0.1"), "ada");
		Assert.Null(limiter.RetryAfter(From("10.0.0.1")));
	}

	[Theory]
	[InlineData(30, "Too many failed sign-ins from your network. Try again in 1 minute.")]
	[InlineData(61, "Too many failed sign-ins from your network. Try again in 2 minutes.")]
	[InlineData(900, "Too many failed sign-ins from your network. Try again in 15 minutes.")]
	public void The_message_rounds_up_to_whole_minutes(int seconds, string message)
	{
		Assert.Equal(message, LoginRateLimiter.Message(TimeSpan.FromSeconds(seconds)));
	}
}
