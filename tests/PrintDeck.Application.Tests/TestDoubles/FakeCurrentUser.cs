using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Tests.TestDoubles;

internal sealed class FakeCurrentUser(CurrentUserInfo? user = null) : ICurrentUser
{
	public static readonly CurrentUserInfo Admin = new("ada", UserRole.Admin);
	public static readonly CurrentUserInfo Viewer = new("vic", UserRole.Viewer);
	public static readonly CurrentUserInfo ApiKey = new("API key", UserRole.Admin, IsApiKey: true);

	public CurrentUserInfo? User { get; set; } = user;

	public ValueTask<CurrentUserInfo?> GetAsync() => ValueTask.FromResult(User);
}
