using System.ComponentModel.DataAnnotations;

namespace PrintDeck.Web.Components.Account;

/// <summary>Form model for a new account (first-run setup). Identity enforces the same rules again on create.</summary>
public sealed class NewAccountInput
{
	[Required(ErrorMessageResourceType = typeof(AccountText), ErrorMessageResourceName = nameof(AccountText.ChooseUsername))]
	[StringLength(64, ErrorMessageResourceType = typeof(AccountText), ErrorMessageResourceName = nameof(AccountText.UsernameTooLong))]
	[RegularExpression(@"^\s*[A-Za-z0-9._@+-]+\s*$", ErrorMessageResourceType = typeof(AccountText), ErrorMessageResourceName = nameof(AccountText.UsernameCharacters))]
	public string UserName { get; set; } = "";

	[Required(ErrorMessageResourceType = typeof(AccountText), ErrorMessageResourceName = nameof(AccountText.ChoosePassword))]
	[StringLength(128, MinimumLength = 8, ErrorMessageResourceType = typeof(AccountText), ErrorMessageResourceName = nameof(AccountText.PasswordTooShort))]
	public string Password { get; set; } = "";

	[Compare(nameof(Password), ErrorMessageResourceType = typeof(AccountText), ErrorMessageResourceName = nameof(AccountText.PasswordsDiffer))]
	public string ConfirmPassword { get; set; } = "";
}
