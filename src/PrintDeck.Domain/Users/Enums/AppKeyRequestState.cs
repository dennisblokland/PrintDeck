namespace PrintDeck.Domain.Users.Enums;

public enum AppKeyRequestState
{
	/// <summary>Waiting for an admin.</summary>
	Pending,

	/// <summary>Allowed: the key is handed out with this answer, and only this once.</summary>
	Granted,

	/// <summary>Denied, expired, already picked up, or never asked.</summary>
	Gone
}
