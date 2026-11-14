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
/// A valid key signs the request in as an admin named "API key"; anything else gets OctoPrint's 403.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISettingsStore settings)
	: AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
	public const string DisplayName = "API key";

	public static string? SuppliedKey(HttpRequest request) =>
		request.Headers["X-Api-Key"].FirstOrDefault() ?? request.Query["apikey"].FirstOrDefault();

	protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		string? supplied = SuppliedKey(Request);
		if (string.IsNullOrEmpty(supplied)) return AuthenticateResult.NoResult();

		PrinterProfile profile = await settings.GetAsync(Context.RequestAborted);
		if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(profile.ApiKey)))
			return AuthenticateResult.Fail("Invalid API key");

		ClaimsIdentity identity = new(
			[new Claim(ClaimTypes.Name, DisplayName), new Claim(ClaimTypes.Role, nameof(UserRole.Admin))],
			AuthSchemes.ApiKey);
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
