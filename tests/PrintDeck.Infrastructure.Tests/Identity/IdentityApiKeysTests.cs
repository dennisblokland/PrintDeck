using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;
using PrintDeck.Infrastructure.Identity;
using PrintDeck.Infrastructure.Tests.TestSupport;

namespace PrintDeck.Infrastructure.Tests.Identity;

/// <summary>App keys in the real Identity database on a SQLite file, wired as the app wires it.</summary>
public sealed class IdentityApiKeysTests : IAsyncLifetime
{
	private readonly TempDirectory _dir = new();
	private ServiceProvider _services = default!;
	private AsyncServiceScope _scope;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private IApiKeys Keys => _scope.ServiceProvider.GetRequiredService<IApiKeys>();
	private IUserAccounts Accounts => _scope.ServiceProvider.GetRequiredService<IUserAccounts>();

	public async ValueTask InitializeAsync()
	{
		IConfiguration config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:DataDirectory"] = Path.Combine(_dir.Path, "data") })
			.Build();
		ServiceCollection services = new();
		services.AddLogging();
		services.AddSingleton(TimeProvider.System);
		services.AddInfrastructure(config);
		services.AddIdentityCore<PrintDeckUser>(IdentityRules.Apply)
			.AddRoles<IdentityRole>()
			.AddEntityFrameworkStores<PrintDeckDbContext>();
		_services = services.BuildServiceProvider();
		await _services.InitializeDatabaseAsync(Ct);
		_scope = _services.CreateAsyncScope();
		Assert.True((await Accounts.CreateAsync("ada", "password123", UserRole.Admin, Ct)).IsSuccess);
		Assert.True((await Accounts.CreateAsync("vic", "password123", UserRole.Viewer, Ct)).IsSuccess);
	}

	public async ValueTask DisposeAsync()
	{
		await _scope.DisposeAsync();
		await _services.DisposeAsync();
		SqliteConnection.ClearAllPools();
		_dir.Dispose();
	}

	/// <summary>A fresh scope, as the next HTTP request gets: nothing cached from the scope that wrote.</summary>
	private async Task<T> InNewScope<T>(Func<IServiceProvider, Task<T>> work)
	{
		await using AsyncServiceScope scope = _services.CreateAsyncScope();
		return await work(scope.ServiceProvider);
	}

	[Fact]
	public async Task A_new_key_signs_in_as_its_owner()
	{
		string key = (await Keys.CreateAsync("ada", "OrcaSlicer", Ct))!;

		ApiKeyOwner? owner = await InNewScope(sp => sp.GetRequiredService<IApiKeys>().FindAsync(key, Ct));

		Assert.Equal(new ApiKeyOwner("OrcaSlicer", "ada", UserRole.Admin), owner);
	}

	[Fact]
	public async Task Only_the_hash_is_stored()
	{
		string key = (await Keys.CreateAsync("ada", "OrcaSlicer", Ct))!;

		PrintDeckApiKey row = await InNewScope(sp => sp.GetRequiredService<PrintDeckDbContext>().ApiKeys.SingleAsync(Ct));

		Assert.Equal(ApiKeySecret.Hash(key), row.Hash);
		Assert.Equal(key[^4..], row.Hint);
		byte[] database = await File.ReadAllBytesAsync(Path.Combine(_dir.Path, "data", "printdeck.db"), Ct);
		Assert.DoesNotContain(key, System.Text.Encoding.UTF8.GetString(database));
	}

	[Fact]
	public async Task Lists_keys_with_owner_and_last_use()
	{
		string orca = (await Keys.CreateAsync("ada", "OrcaSlicer", Ct))!;
		await Keys.CreateAsync("vic", "Dashboard", Ct);
		await InNewScope(sp => sp.GetRequiredService<IApiKeys>().FindAsync(orca, Ct));

		IReadOnlyList<ApiKeyInfo> keys = await InNewScope(sp => sp.GetRequiredService<IApiKeys>().ListAsync(Ct));

		Assert.Equal(["OrcaSlicer", "Dashboard"], keys.Select(k => k.Name));
		Assert.Equal(["ada", "vic"], keys.Select(k => k.OwnerName));
		Assert.NotNull(keys[0].LastUsedAt);
		Assert.Null(keys[1].LastUsedAt);
	}

	[Fact]
	public async Task Unknown_owners_and_keys_find_nothing()
	{
		Assert.Null(await Keys.CreateAsync("ghost", "OrcaSlicer", Ct));
		Assert.Null(await Keys.FindAsync("NOT-A-KEY", Ct));
	}

	[Fact]
	public async Task A_revoked_key_stops_working()
	{
		string key = (await Keys.CreateAsync("ada", "OrcaSlicer", Ct))!;
		string id = Assert.Single(await Keys.ListAsync(Ct)).Id;

		Assert.True(await Keys.RevokeAsync(id, Ct));
		Assert.False(await Keys.RevokeAsync(id, Ct));

		Assert.Null(await InNewScope(sp => sp.GetRequiredService<IApiKeys>().FindAsync(key, Ct)));
	}

	[Fact]
	public async Task A_key_follows_its_owners_role()
	{
		string key = (await Keys.CreateAsync("ada", "OrcaSlicer", Ct))!;

		await Accounts.CreateAsync("bob", "password123", UserRole.Admin, Ct); // PrintDeck keeps one admin
		Assert.True((await Accounts.SetRoleAsync("ada", UserRole.Viewer, Ct)).IsSuccess);

		Assert.Equal(UserRole.Viewer, (await InNewScope(sp => sp.GetRequiredService<IApiKeys>().FindAsync(key, Ct)))?.Role);
	}

	[Fact]
	public async Task Deleting_an_account_deletes_its_keys()
	{
		string key = (await Keys.CreateAsync("vic", "Dashboard", Ct))!;
		await Keys.CreateAsync("ada", "OrcaSlicer", Ct);

		Assert.True((await InNewScope(sp => sp.GetRequiredService<IUserAccounts>().DeleteAsync("vic", Ct))).IsSuccess);

		Assert.Null(await InNewScope(sp => sp.GetRequiredService<IApiKeys>().FindAsync(key, Ct)));
		Assert.Equal(["OrcaSlicer"], (await InNewScope(sp => sp.GetRequiredService<IApiKeys>().ListAsync(Ct))).Select(k => k.Name));
	}
}
