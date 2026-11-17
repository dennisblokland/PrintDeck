using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Infrastructure.Identity;

/// <summary><see cref="IApiKeys"/> in the Identity database, next to the accounts the keys act as.</summary>
public sealed class IdentityApiKeys(PrintDeckDbContext db, UserManager<PrintDeckUser> users, TimeProvider time) : IApiKeys
{
	/// <summary>"Last used" moves at most this often, so a busy slicer doesn't write to the database on every request.</summary>
	private static readonly TimeSpan _lastUsedStep = TimeSpan.FromMinutes(1);

	public async Task<IReadOnlyList<ApiKeyInfo>> ListAsync(CancellationToken ct)
	{
		List<ApiKeyInfo> keys = await db.ApiKeys.AsNoTracking()
			.Select(k => new ApiKeyInfo(k.Id, k.Name, k.User!.UserName!, k.Hint, k.CreatedAt, k.LastUsedAt))
			.ToListAsync(ct);
		// SQLite can't sort DateTimeOffset; there are only a handful of keys.
		return keys.OrderBy(k => k.CreatedAt).ThenBy(k => k.Name, StringComparer.OrdinalIgnoreCase).ToList();
	}

	public async Task<string?> CreateAsync(string ownerName, string name, CancellationToken ct)
	{
		if (await users.FindByNameAsync(ownerName) is not { } owner) return null;
		string key = ApiKeySecret.New();
		db.ApiKeys.Add(new PrintDeckApiKey
		{
			UserId = owner.Id,
			Name = name,
			Hash = ApiKeySecret.Hash(key),
			Hint = ApiKeySecret.Hint(key),
			CreatedAt = time.GetUtcNow()
		});
		await db.SaveChangesAsync(ct);
		return key;
	}

	public async Task<bool> RevokeAsync(string id, CancellationToken ct) =>
		await db.ApiKeys.Where(k => k.Id == id).ExecuteDeleteAsync(ct) > 0;

	public async Task<ApiKeyOwner?> FindAsync(string key, CancellationToken ct)
	{
		string hash = ApiKeySecret.Hash(key);
		PrintDeckApiKey? row = await db.ApiKeys.Include(k => k.User).FirstOrDefaultAsync(k => k.Hash == hash, ct);
		if (row?.User is not { UserName: { } userName } owner) return null;

		DateTimeOffset now = time.GetUtcNow();
		if (row.LastUsedAt is not { } last || now - last >= _lastUsedStep)
		{
			row.LastUsedAt = now;
			await db.SaveChangesAsync(ct);
		}

		// The owner's role now, not when the key was made: demoting an admin also stops their keys from uploading.
		UserRole role = await users.IsInRoleAsync(owner, nameof(UserRole.Admin)) ? UserRole.Admin : UserRole.Viewer;
		return new ApiKeyOwner(row.Name, userName, role);
	}
}
