using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Abstractions;

/// <summary>The person (or slicer, when <see cref="IsApiKey"/>) behind the current command or query.</summary>
public sealed record CurrentUserInfo(string UserName, UserRole Role, bool IsApiKey = false)
{
	public bool IsAdmin => Role == UserRole.Admin;
}
