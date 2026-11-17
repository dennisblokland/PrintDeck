using Microsoft.Extensions.Options;

using PrintDeck.Domain.Users;

namespace PrintDeck.Web.Auth;

/// <summary>
/// The login page's <see cref="LoginThrottle"/>, keyed by the address the request came from. Behind a reverse proxy
/// that is the proxy's address, so everyone shares one limit unless forwarded headers are set up.
/// </summary>
public sealed class LoginRateLimiter(IOptions<LoginRateLimitOptions> options, TimeProvider time, ILogger<LoginRateLimiter> logger)
{
	private readonly LoginThrottle? _throttle = options.Value.MaxFailures > 0
		? new LoginThrottle(options.Value.MaxFailures, TimeSpan.FromMinutes(Math.Max(1, options.Value.WindowMinutes)))
		: null;

	/// <summary>How long this request's address must wait before signing in again; null when it may try now.</summary>
	public TimeSpan? RetryAfter(HttpContext context) =>
		_throttle?.RetryAfter(LoginThrottle.ClientKey(context.Connection.RemoteIpAddress), time.GetUtcNow());

	public void RecordFailure(HttpContext context, string userName)
	{
		if (_throttle is null) return;
		string client = LoginThrottle.ClientKey(context.Connection.RemoteIpAddress);
		DateTimeOffset now = time.GetUtcNow();
		_throttle.RecordFailure(client, now);
		if (_throttle.RetryAfter(client, now) is not null)
			logger.LogWarning("Sign-ins from {Client} paused for {Minutes} min after {Failures} failures (last for {UserName})",
				client, _throttle.Window.TotalMinutes, _throttle.MaxFailures, userName);
	}

	/// <summary>The message the login page shows, rounded up to whole minutes.</summary>
	public static string Message(TimeSpan wait)
	{
		int minutes = Math.Max(1, (int)Math.Ceiling(wait.TotalMinutes));
		return $"Too many failed sign-ins from your network. Try again in {minutes} minute{(minutes == 1 ? "" : "s")}.";
	}
}
