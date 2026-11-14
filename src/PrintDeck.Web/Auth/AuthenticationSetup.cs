using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Infrastructure.Identity;
using PrintDeck.Infrastructure.Storage;

namespace PrintDeck.Web.Auth;

public static class AuthenticationSetup
{
	/// <summary>
	/// ASP.NET Core Identity (accounts in SQLite) with its login cookie, plus the slicer API key as a second scheme.
	/// Everything requires a signed-in user unless an endpoint or page says <c>[AllowAnonymous]</c>.
	/// </summary>
	public static IServiceCollection AddPrintDeckAuthentication(this IServiceCollection services)
	{
		services.AddHttpContextAccessor();
		services.AddCascadingAuthenticationState();
		services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
		services.AddScoped<ICurrentUser, CurrentUser>();

		services.AddAuthentication(o =>
			{
				o.DefaultScheme = AuthSchemes.Auto;
				o.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
			})
			.AddPolicyScheme(AuthSchemes.Auto, null, o => o.ForwardDefaultSelector = ctx =>
				ApiKeyAuthenticationHandler.SuppliedKey(ctx.Request) is { Length: > 0 } ? AuthSchemes.ApiKey : IdentityConstants.ApplicationScheme)
			.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(AuthSchemes.ApiKey, null)
			.AddIdentityCookies();

		services.AddIdentityCore<PrintDeckUser>(IdentityRules.Apply)
			.AddRoles<IdentityRole>()
			.AddEntityFrameworkStores<PrintDeckDbContext>()
			.AddSignInManager();

		services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));
		services.ConfigureApplicationCookie(o =>
		{
			o.Cookie.Name = "PrintDeck.Auth";
			o.Cookie.HttpOnly = true;
			o.Cookie.SameSite = SameSiteMode.Lax;
			o.LoginPath = "/login";
			o.AccessDeniedPath = "/access-denied";
			o.ExpireTimeSpan = TimeSpan.FromDays(14);
			o.SlidingExpiration = true;
			o.Events.OnRedirectToLogin = RedirectToLoginOrSetup;
		});

		// Keys that sign the cookie live in the data directory, so restarts keep people signed in.
		services.AddDataProtection().SetApplicationName("PrintDeck");
		services.AddOptions<KeyManagementOptions>()
			.Configure<IOptions<StorageOptions>, ILoggerFactory>((o, storage, logs) =>
				o.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(storage.Value.KeysDirectory), logs));

		services.AddAuthorizationBuilder()
			.SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
			.AddPolicy(AuthSchemes.ApiKeyPolicy, p => p.AddAuthenticationSchemes(AuthSchemes.ApiKey).RequireAuthenticatedUser());

		return services;
	}

	/// <summary>Browsers go to the login page (or first-run setup); fetches and scripts get a plain 401.</summary>
	private static async Task RedirectToLoginOrSetup(RedirectContext<CookieAuthenticationOptions> ctx)
	{
		HttpRequest request = ctx.Request;
		bool wantsPage = HttpMethods.IsGet(request.Method) && request.Headers.Accept.Any(a => a?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true);
		if (!wantsPage)
		{
			ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return;
		}

		IUserAccounts accounts = ctx.HttpContext.RequestServices.GetRequiredService<IUserAccounts>();
		ctx.Response.Redirect(await accounts.AnyAsync(ctx.HttpContext.RequestAborted) ? ctx.RedirectUri : $"{request.PathBase}/setup");
	}

	public static void MapAccountEndpoints(this WebApplication app) =>
		// A form field makes minimal APIs validate the antiforgery token, so other sites can't log you out.
		app.MapPost("/account/logout", async (SignInManager<PrintDeckUser> signIn, [FromForm] string? returnUrl) =>
		{
			await signIn.SignOutAsync();
			return TypedResults.LocalRedirect("~/login");
		});
}
