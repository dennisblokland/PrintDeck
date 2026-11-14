using System.ComponentModel.DataAnnotations;

namespace PrintDeck.Web.Components.Account;

/// <summary>Form model for a new account (first-run setup). Identity enforces the same rules again on create.</summary>
public sealed class NewAccountInput
{
	[Required(ErrorMessage = "Choose a username.")]
	[StringLength(64, ErrorMessage = "Keep the username under 64 characters.")]
	[RegularExpression(@"^\s*[A-Za-z0-9._@+-]+\s*$", ErrorMessage = "Use letters, digits and . _ - @ + only.")]
	public string UserName { get; set; } = "";

	[Required(ErrorMessage = "Choose a password.")]
	[StringLength(128, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
	public string Password { get; set; } = "";

	[Compare(nameof(Password), ErrorMessage = "The passwords don't match.")]
	public string ConfirmPassword { get; set; } = "";
}
