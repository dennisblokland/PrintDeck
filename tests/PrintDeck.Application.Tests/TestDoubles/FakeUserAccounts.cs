using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Tests.TestDoubles;

/// <summary>Accounts in a list; passwords must be 8+ characters, like the real rules.</summary>
internal sealed class FakeUserAccounts : IUserAccounts
{
	private readonly List<(UserAccount Account, string Password)> _users = [];

	public IReadOnlyList<UserAccount> Users => _users.Select(u => u.Account).ToList();
	public string? PasswordOf(string userName) => Index(userName) is var i and >= 0 ? _users[i].Password : null;

	public FakeUserAccounts With(string userName, UserRole role, string password = "password123")
	{
		_users.Add((new UserAccount(userName, role, false), password));
		return this;
	}

	public Task<bool> AnyAsync(CancellationToken ct) => Task.FromResult(_users.Count > 0);
	public Task<IReadOnlyList<UserAccount>> ListAsync(CancellationToken ct) => Task.FromResult(Users);

	public Task<Result> CreateAsync(string userName, string password, UserRole role, CancellationToken ct)
	{
		if (Index(userName) >= 0) return Task.FromResult(Result.Failure($"Username '{userName}' is already taken."));
		if (password.Length < 8) return Task.FromResult(Result.Failure("Passwords must be at least 8 characters."));
		With(userName, role, password);
		return Task.FromResult(Result.Success());
	}

	public Task<Result> SetRoleAsync(string userName, UserRole role, CancellationToken ct) =>
		Update(userName, u => (u.Account with { Role = role }, u.Password));

	public Task<Result> ResetPasswordAsync(string userName, string newPassword, CancellationToken ct) =>
		Update(userName, u => (u.Account with { IsLockedOut = false }, newPassword));

	public Task<Result> ChangePasswordAsync(string userName, string currentPassword, string newPassword, CancellationToken ct) =>
		PasswordOf(userName) == currentPassword
			? Update(userName, u => (u.Account, newPassword))
			: Task.FromResult(Result.Failure("Current password is wrong."));

	public Task<Result> DeleteAsync(string userName, CancellationToken ct)
	{
		int i = Index(userName);
		if (i < 0) return Task.FromResult(Result.Failure($"No user named {userName}."));
		_users.RemoveAt(i);
		return Task.FromResult(Result.Success());
	}

	private Task<Result> Update(string userName, Func<(UserAccount Account, string Password), (UserAccount, string)> change)
	{
		int i = Index(userName);
		if (i < 0) return Task.FromResult(Result.Failure($"No user named {userName}."));
		_users[i] = change(_users[i]);
		return Task.FromResult(Result.Success());
	}

	private int Index(string userName) =>
		_users.FindIndex(u => string.Equals(u.Account.UserName, userName, StringComparison.OrdinalIgnoreCase));
}
