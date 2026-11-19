using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace PrintDeck.Web.Components.Account;

/// <summary>
/// Validation messages of the account forms, from <c>Resources/Components/Account/AccountText.resx</c>. Data annotations
/// take a type and a property name (<c>ErrorMessageResourceType</c>), not an <c>IStringLocalizer</c>, hence the properties.
/// </summary>
public static class AccountText
{
	private static readonly ResourceManager _resources = new("PrintDeck.Web.Resources.Components.Account.AccountText", typeof(AccountText).Assembly);

	private static string Get([CallerMemberName] string key = "") => _resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

	public static string ChooseUsername => Get();
	public static string UsernameTooLong => Get();
	public static string UsernameCharacters => Get();
	public static string ChoosePassword => Get();
	public static string PasswordTooShort => Get();
	public static string PasswordsDiffer => Get();
	public static string EnterUsername => Get();
	public static string EnterPassword => Get();
	public static string EnterCurrentPassword => Get();
	public static string ChooseNewPassword => Get();
}
