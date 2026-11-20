namespace PrintDeck.Application.Abstractions;

/// <summary>
/// Who a scope acts for when there is no request or circuit: set once on a scope PrintDeck creates itself (a module's
/// own context). <see cref="Interfaces.ICurrentUser"/> returns it before looking at the request.
/// </summary>
public sealed class ScopeIdentity
{
	public CurrentUserInfo? User { get; set; }
}
