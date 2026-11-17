using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Settings;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Web.Auth;

/// <summary>
/// The slicer's "send to printer" key, as OctoPrint takes it: <c>X-Api-Key</c> header or <c>?apikey=</c>.
/// An app key signs the request in as its owner's account, with that account's role, named like "OrcaSlicer (ada)".
/// The shared key, while it is switched on, signs in as an admin named "API key". Anything else gets OctoPrint's 403.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISettingsStore settings, IApiKeys keys)
	: AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
	public const string DisplayName = "API key";

	public static string? SuppliedKey(HttpRequest request) =>
		request.Headers["X-Api-Key"].FirstOrDefault() ?? request.Query["apikey"].FirstOrDefault();

	protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		string? supplied = SuppliedKey(Request);
		if (string.IsNullOrEmpty(supplied)) return AuthenticateResult.NoResult();

		HostSettings s = await settings.GetAsync(Context.RequestAborted);
		if (s.SharedApiKeyEnabled && s.ApiKey.Length > 0
			&& CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(s.ApiKey)))
			return SignIn(DisplayName, UserRole.Admin);

		return await keys.FindAsync(supplied, Context.RequestAborted) is { } owner
			? SignIn(owner.DisplayName, owner.Role)
			: AuthenticateResult.Fail("Invalid API key");
	}

	private static AuthenticateResult SignIn(string name, UserRole role)
	{
		ClaimsIdentity identity = new([new Claim(ClaimTypes.Name, name), new Claim(ClaimTypes.Role, role.ToString())], AuthSchemes.ApiKey);
		return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), AuthSchemes.ApiKey));
	}

	protected override Task HandleChallengeAsync(AuthenticationProperties properties) => Reject();
	protected override Task HandleForbiddenAsync(AuthenticationProperties properties) => Reject();

	private Task Reject()
	{
		Response.StatusCode = StatusCodes.Status403Forbidden;
		return Response.WriteAsJsonAsync(new { error = "Invalid API key" });
	}
}
