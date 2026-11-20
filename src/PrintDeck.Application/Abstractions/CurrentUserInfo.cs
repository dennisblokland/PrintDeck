using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Abstractions;

/// <summary>
/// The person (or slicer, when <see cref="IsApiKey"/>, or module, when <see cref="Module"/> is set) behind the
/// current command or query.
/// </summary>
/// <param name="Module">The id of the module acting on its own, outside any request (see <c>Plugins/ModuleHost</c>).</param>
public sealed record CurrentUserInfo(string UserName, UserRole Role, bool IsApiKey = false, string? Module = null)
{
	public bool IsAdmin => Role == UserRole.Admin;
	public bool IsModule => Module is not null;

	/// <summary>The identity a module's own context acts as: allowed to control the printer, never to manage accounts or the host.</summary>
	public static CurrentUserInfo ForModule(string moduleId) => new($"module:{moduleId}", UserRole.Admin, Module: moduleId);
}
