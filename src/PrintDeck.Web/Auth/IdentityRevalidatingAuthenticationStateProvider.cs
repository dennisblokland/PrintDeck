using System.Security.Claims;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PrintDeck.Infrastructure.Identity;

namespace PrintDeck.Web.Auth;

/// <summary>
/// Re-checks the security stamp of an open Blazor circuit every minute, so a deleted user, a password change
/// or a role change ends sessions that are already open, not only new page loads.
/// </summary>
internal sealed class IdentityRevalidatingAuthenticationStateProvider(
	ILoggerFactory loggerFactory, IServiceScopeFactory scopes, IOptions<IdentityOptions> options)
	: RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
	protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

	protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState state, CancellationToken cancellationToken)
	{
		await using AsyncServiceScope scope = scopes.CreateAsyncScope();
		UserManager<PrintDeckUser> users = scope.ServiceProvider.GetRequiredService<UserManager<PrintDeckUser>>();
		if (await users.GetUserAsync(state.User) is not { } user) return false;
		if (!users.SupportsUserSecurityStamp) return true;

		string? stamp = state.User.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);
		return stamp == await users.GetSecurityStampAsync(user);
	}
}
