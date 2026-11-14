using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Features.Users;

internal static class UserRules
{
	public static UserAccount? Find(IReadOnlyList<UserAccount> users, string userName) =>
		users.FirstOrDefault(u => string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase));

	public static int AdminCount(IReadOnlyList<UserAccount> users) => users.Count(u => u.Role == UserRole.Admin);
}
