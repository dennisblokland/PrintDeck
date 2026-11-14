using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;
using PrintDeck.Infrastructure.Identity;
using PrintDeck.Infrastructure.Tests.TestSupport;

namespace PrintDeck.Infrastructure.Tests.Identity;

/// <summary>The real Identity stack on a SQLite file in a temp directory, wired as the app wires it.</summary>
public sealed class IdentityUserAccountsTests : IAsyncLifetime
{
	private readonly TempDirectory _dir = new();
	private ServiceProvider _services = default!;
	private AsyncServiceScope _scope;
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private IUserAccounts Accounts => _scope.ServiceProvider.GetRequiredService<IUserAccounts>();
	private UserManager<PrintDeckUser> Users => _scope.ServiceProvider.GetRequiredService<UserManager<PrintDeckUser>>();

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
		await _services.InitializeDatabaseAsync(TestContext.Current.CancellationToken);
		_scope = _services.CreateAsyncScope();
	}

	public async ValueTask DisposeAsync()
	{
		await _scope.DisposeAsync();
		await _services.DisposeAsync();
		SqliteConnection.ClearAllPools();
		_dir.Dispose();
	}

	private async Task<bool> PasswordWorks(string userName, string password) =>
		await Users.FindByNameAsync(userName) is { } u && await Users.CheckPasswordAsync(u, password);

	[Fact]
	public async Task Starts_empty_and_lists_users_with_their_roles()
	{
		Assert.False(await Accounts.AnyAsync(Ct));

		Assert.True((await Accounts.CreateAsync("vic", "password123", UserRole.Viewer, Ct)).IsSuccess);
		Assert.True((await Accounts.CreateAsync("ada", "password123", UserRole.Admin, Ct)).IsSuccess);

		Assert.True(await Accounts.AnyAsync(Ct));
		Assert.Equal(
			[new UserAccount("ada", UserRole.Admin, false), new UserAccount("vic", UserRole.Viewer, false)],
			await Accounts.ListAsync(Ct));
	}

	[Fact]
	public async Task Initialising_twice_keeps_the_data()
	{
		await Accounts.CreateAsync("ada", "password123", UserRole.Admin, Ct);
		await _services.InitializeDatabaseAsync(Ct);
		Assert.Single(await Accounts.ListAsync(Ct));
	}

	[Fact]
	public async Task Rejects_duplicate_names_and_short_passwords_with_identity_messages()
	{
		await Accounts.CreateAsync("ada", "password123", UserRole.Admin, Ct);

		Result duplicate = await Accounts.CreateAsync("ADA", "password123", UserRole.Viewer, Ct);
		Result shortPassword = await Accounts.CreateAsync("bob", "short", UserRole.Viewer, Ct);

		Assert.Equal("Username 'ADA' is already taken.", duplicate.Error);
		Assert.Equal("Passwords must be at least 8 characters.", shortPassword.Error);
		Assert.Single(await Accounts.ListAsync(Ct));
	}

	[Fact]
	public async Task Changes_role_and_invalidates_existing_sessions()
	{
		await Accounts.CreateAsync("vic", "password123", UserRole.Viewer, Ct);
		string? stamp = (await Users.FindByNameAsync("vic"))!.SecurityStamp;

		Assert.True((await Accounts.SetRoleAsync("vic", UserRole.Admin, Ct)).IsSuccess);

		Assert.Equal(UserRole.Admin, Assert.Single(await Accounts.ListAsync(Ct)).Role);
		Assert.NotEqual(stamp, (await Users.FindByNameAsync("vic"))!.SecurityStamp);
	}

	[Fact]
	public async Task Change_password_checks_the_current_one()
	{
		await Accounts.CreateAsync("ada", "password123", UserRole.Admin, Ct);

		Assert.Equal("Current password is wrong.", (await Accounts.ChangePasswordAsync("ada", "nope", "new-password", Ct)).Error);
		Assert.True((await Accounts.ChangePasswordAsync("ada", "password123", "new-password", Ct)).IsSuccess);
		Assert.True(await PasswordWorks("ada", "new-password"));
	}

	[Fact]
	public async Task Reset_password_sets_a_new_one_and_lifts_the_lockout()
	{
		await Accounts.CreateAsync("vic", "password123", UserRole.Viewer, Ct);
		PrintDeckUser vic = (await Users.FindByNameAsync("vic"))!;
		for (int i = 0; i < 5; i++) await Users.AccessFailedAsync(vic);
		Assert.True(Assert.Single(await Accounts.ListAsync(Ct)).IsLockedOut);

		Assert.True((await Accounts.ResetPasswordAsync("vic", "fresh-password", Ct)).IsSuccess);

		Assert.False(Assert.Single(await Accounts.ListAsync(Ct)).IsLockedOut);
		Assert.True(await PasswordWorks("vic", "fresh-password"));
	}

	[Fact]
	public async Task A_rejected_reset_keeps_the_old_password()
	{
		await Accounts.CreateAsync("vic", "password123", UserRole.Viewer, Ct);

		Assert.Equal("Passwords must be at least 8 characters.", (await Accounts.ResetPasswordAsync("vic", "short", Ct)).Error);
		Assert.True(await PasswordWorks("vic", "password123"));
	}

	[Fact]
	public async Task Deletes_users_and_reports_unknown_ones()
	{
		await Accounts.CreateAsync("vic", "password123", UserRole.Viewer, Ct);

		Assert.True((await Accounts.DeleteAsync("vic", Ct)).IsSuccess);
		Assert.Equal("No user named vic.", (await Accounts.DeleteAsync("vic", Ct)).Error);
		Assert.False(await Accounts.AnyAsync(Ct));
	}
}
