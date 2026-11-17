namespace PrintDeck.Web.Auth;

/// <summary>
/// Failed sign-ins allowed per client address, from the <c>LoginRateLimit</c> section of appsettings.json
/// (or environment variables such as <c>LoginRateLimit__MaxFailures</c>).
/// </summary>
public sealed class LoginRateLimitOptions
{
	public const string Section = "LoginRateLimit";

	/// <summary>Failures inside the window before the address has to wait. 0 switches the limit off.</summary>
	public int MaxFailures { get; set; } = 10;

	public int WindowMinutes { get; set; } = 15;
}
