namespace PrintDeck.Domain.Users.Enums;

/// <summary>What a signed-in person may do: admins run the printer and change settings, viewers only watch.</summary>
public enum UserRole
{
	Viewer = 0,
	Admin = 1
}
