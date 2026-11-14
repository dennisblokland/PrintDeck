using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Domain.Users;

public sealed record UserAccount(string UserName, UserRole Role, bool IsLockedOut);
