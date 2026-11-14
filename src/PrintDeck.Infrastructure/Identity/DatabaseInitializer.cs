using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using PrintDeck.Domain.Users.Enums;
using PrintDeck.Infrastructure.Storage;

namespace PrintDeck.Infrastructure.Identity;

public static class DatabaseInitializer
{
	/// <summary>Creates or upgrades printdeck.db and makes sure every <see cref="UserRole"/> exists as an Identity role.</summary>
	public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
	{
		await using AsyncServiceScope scope = services.CreateAsyncScope();
		IServiceProvider sp = scope.ServiceProvider;

		Directory.CreateDirectory(sp.GetRequiredService<IOptions<StorageOptions>>().Value.DataDirectory);
		await sp.GetRequiredService<PrintDeckDbContext>().Database.MigrateAsync(ct);

		RoleManager<IdentityRole> roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
		foreach (string role in Enum.GetNames<UserRole>())
		{
			if (await roles.RoleExistsAsync(role)) continue;
			IdentityResult created = await roles.CreateAsync(new IdentityRole(role));
			if (!created.Succeeded)
				throw new InvalidOperationException($"Could not create role {role}: {string.Join(" ", created.Errors.Select(e => e.Description))}");
		}
	}
}
