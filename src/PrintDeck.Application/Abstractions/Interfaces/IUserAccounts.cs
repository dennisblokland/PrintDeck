using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Abstractions.Interfaces;

/// <summary>User accounts and their passwords. Failures carry a message fit to show the user.</summary>
public interface IUserAccounts
{
	Task<bool> AnyAsync(CancellationToken ct);
	Task<IReadOnlyList<UserAccount>> ListAsync(CancellationToken ct);
	Task<Result> CreateAsync(string userName, string password, UserRole role, CancellationToken ct);
	Task<Result> SetRoleAsync(string userName, UserRole role, CancellationToken ct);
	/// <summary>Sets a new password without the old one and lifts any lockout.</summary>
	Task<Result> ResetPasswordAsync(string userName, string newPassword, CancellationToken ct);
	Task<Result> ChangePasswordAsync(string userName, string currentPassword, string newPassword, CancellationToken ct);
	Task<Result> DeleteAsync(string userName, CancellationToken ct);
}
