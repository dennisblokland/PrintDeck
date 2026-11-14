using System.Security.Claims;

using Microsoft.AspNetCore.Components.Authorization;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Web.Auth;

/// <summary>
/// <see cref="ICurrentUser"/> for both kinds of scope: in a Blazor circuit (and during prerendering) the
/// <see cref="AuthenticationStateProvider"/> knows the user and tells us when that changes (sign-out,
/// failed revalidation); in a plain HTTP request (slicer API, toolpath) it isn't initialised, so the
/// request's own principal is used.
/// </summary>
public sealed class CurrentUser : ICurrentUser, IDisposable
{
	private readonly AuthenticationStateProvider _authState;
	private readonly IHttpContextAccessor _http;
	private Task<AuthenticationState>? _changed;

	public CurrentUser(AuthenticationStateProvider authState, IHttpContextAccessor http)
	{
		_authState = authState;
		_http = http;
		_authState.AuthenticationStateChanged += OnChanged;
	}

	private void OnChanged(Task<AuthenticationState> state) => _changed = state;

	public async ValueTask<CurrentUserInfo?> GetAsync()
	{
		ClaimsPrincipal? principal;
		try { principal = (await (_changed ?? _authState.GetAuthenticationStateAsync())).User; }
		catch (InvalidOperationException) { principal = _http.HttpContext?.User; }
		return From(principal);
	}

	internal static CurrentUserInfo? From(ClaimsPrincipal? principal) =>
		principal?.Identity is { IsAuthenticated: true, Name: { Length: > 0 } name } identity
			? new CurrentUserInfo(
				name,
				principal.IsInRole(nameof(UserRole.Admin)) ? UserRole.Admin : UserRole.Viewer,
				identity.AuthenticationType == AuthSchemes.ApiKey)
			: null;

	public void Dispose() => _authState.AuthenticationStateChanged -= OnChanged;
}
