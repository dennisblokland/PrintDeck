using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Infrastructure.Identity;

/// <summary><see cref="IUserAccounts"/> on ASP.NET Core Identity's <see cref="UserManager{TUser}"/>.</summary>
public sealed class IdentityUserAccounts(UserManager<PrintDeckUser> users, TimeProvider time) : IUserAccounts
{
	public Task<bool> AnyAsync(CancellationToken ct) => users.Users.AnyAsync(ct);

	public async Task<IReadOnlyList<UserAccount>> ListAsync(CancellationToken ct)
	{
		List<PrintDeckUser> all = await users.Users.OrderBy(u => u.NormalizedUserName).ToListAsync(ct);
		HashSet<string> admins = (await users.GetUsersInRoleAsync(nameof(UserRole.Admin))).Select(u => u.Id).ToHashSet();
		DateTimeOffset now = time.GetUtcNow();
		return all.Select(u => new UserAccount(
			u.UserName!,
			admins.Contains(u.Id) ? UserRole.Admin : UserRole.Viewer,
			u.LockoutEnd is { } end && end > now)).ToList();
	}

	public async Task<Result> CreateAsync(string userName, string password, UserRole role, CancellationToken ct)
	{
		PrintDeckUser user = new() { UserName = userName };
		IdentityResult created = await users.CreateAsync(user, password);
		if (!created.Succeeded) return Failure(created);

		IdentityResult added = await users.AddToRoleAsync(user, role.ToString());
		if (added.Succeeded) return Result.Success();
		await users.DeleteAsync(user);
		return Failure(added);
	}

	public async Task<Result> SetRoleAsync(string userName, UserRole role, CancellationToken ct)
	{
		if (await users.FindByNameAsync(userName) is not { } user) return NotFound(userName);

		IdentityResult removed = await users.RemoveFromRolesAsync(user, await users.GetRolesAsync(user));
		if (!removed.Succeeded) return Failure(removed);
		IdentityResult added = await users.AddToRoleAsync(user, role.ToString());
		if (!added.Succeeded) return Failure(added);
		// Signs the user out everywhere, so their next request picks up the new role.
		return From(await users.UpdateSecurityStampAsync(user));
	}

	public async Task<Result> ResetPasswordAsync(string userName, string newPassword, CancellationToken ct)
	{
		if (await users.FindByNameAsync(userName) is not { } user) return NotFound(userName);

		// Validate first: removing the old password and then failing would leave the account without one.
		foreach (IPasswordValidator<PrintDeckUser> validator in users.PasswordValidators)
		{
			IdentityResult valid = await validator.ValidateAsync(users, user, newPassword);
			if (!valid.Succeeded) return Failure(valid);
		}

		if (await users.HasPasswordAsync(user))
		{
			IdentityResult removed = await users.RemovePasswordAsync(user);
			if (!removed.Succeeded) return Failure(removed);
		}
		IdentityResult added = await users.AddPasswordAsync(user, newPassword);
		if (!added.Succeeded) return Failure(added);

		await users.SetLockoutEndDateAsync(user, null);
		return From(await users.ResetAccessFailedCountAsync(user));
	}

	public async Task<Result> ChangePasswordAsync(string userName, string currentPassword, string newPassword, CancellationToken ct)
	{
		if (await users.FindByNameAsync(userName) is not { } user) return NotFound(userName);
		IdentityResult changed = await users.ChangePasswordAsync(user, currentPassword, newPassword);
		if (changed.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
			return Result.Failure("Current password is wrong.");
		return From(changed);
	}

	public async Task<Result> DeleteAsync(string userName, CancellationToken ct) =>
		await users.FindByNameAsync(userName) is { } user ? From(await users.DeleteAsync(user)) : NotFound(userName);

	private static Result NotFound(string userName) => Result.Failure($"No user named {userName}.");
	private static Result From(IdentityResult r) => r.Succeeded ? Result.Success() : Failure(r);
	private static Result Failure(IdentityResult r) => Result.Failure(string.Join(" ", r.Errors.Select(e => e.Description)));
}
