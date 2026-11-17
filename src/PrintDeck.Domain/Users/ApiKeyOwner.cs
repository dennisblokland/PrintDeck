using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Domain.Users;

/// <summary>Who a valid app key signs in as: its owner's account and current role.</summary>
public sealed record ApiKeyOwner(string KeyName, string UserName, UserRole Role)
{
	/// <summary>The name logs and the UI show for requests made with this key, e.g. "OrcaSlicer (ada)".</summary>
	public string DisplayName => $"{KeyName} ({UserName})";
}
