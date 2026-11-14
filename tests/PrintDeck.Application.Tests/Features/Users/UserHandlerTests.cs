using PrintDeck.Application.Features.Users.Commands;
using PrintDeck.Application.Features.Users.Handlers;
using PrintDeck.Application.Features.Users.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Tests.Features.Users;

public class UserHandlerTests
{
	private readonly FakeUserAccounts _accounts = new();
	private readonly FakeCurrentUser _me = new(FakeCurrentUser.Admin);
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task Setup_is_required_until_the_first_account_exists()
	{
		IsSetupRequiredHandler handler = new(_accounts);
		Assert.True(await handler.Handle(new IsSetupRequiredQuery(), Ct));

		_accounts.With("ada", UserRole.Admin);
		Assert.False(await handler.Handle(new IsSetupRequiredQuery(), Ct));
	}

	[Fact]
	public async Task First_admin_is_created_once()
	{
		CreateFirstAdminHandler handler = new(_accounts);

		Assert.True((await handler.Handle(new CreateFirstAdminCommand(" ada ", "password123"), Ct)).IsSuccess);
		Result second = await handler.Handle(new CreateFirstAdminCommand("mallory", "password123"), Ct);

		Assert.Equal("PrintDeck is already set up. Sign in instead.", second.Error);
		Assert.Equal(("ada", UserRole.Admin), (Assert.Single(_accounts.Users).UserName, _accounts.Users[0].Role));
	}

	[Fact]
	public async Task The_last_admin_cannot_be_demoted()
	{
		_accounts.With("ada", UserRole.Admin).With("vic", UserRole.Viewer);
		SetUserRoleHandler handler = new(_accounts);

		Result demote = await handler.Handle(new SetUserRoleCommand("ada", UserRole.Viewer), Ct);
		Assert.Equal("PrintDeck needs at least one admin. Make someone else admin first.", demote.Error);

		Assert.True((await handler.Handle(new SetUserRoleCommand("VIC", UserRole.Admin), Ct)).IsSuccess);
		Assert.True((await handler.Handle(new SetUserRoleCommand("ada", UserRole.Viewer), Ct)).IsSuccess);
		Assert.Equal([UserRole.Viewer, UserRole.Admin], _accounts.Users.Select(u => u.Role));
	}

	[Fact]
	public async Task Role_changes_for_unknown_users_fail()
	{
		Result r = await new SetUserRoleHandler(_accounts).Handle(new SetUserRoleCommand("ghost", UserRole.Admin), Ct);
		Assert.Equal("No user named ghost.", r.Error);
	}

	[Fact]
	public async Task You_cannot_delete_yourself()
	{
		_accounts.With("ada", UserRole.Admin).With("bob", UserRole.Admin);

		Result r = await new DeleteUserHandler(_accounts, _me).Handle(new DeleteUserCommand("ADA"), Ct);

		Assert.Equal("You can't delete your own account.", r.Error);
		Assert.Equal(2, _accounts.Users.Count);
	}

	[Fact]
	public async Task The_last_admin_cannot_be_deleted()
	{
		// Only reachable through the API key, which has no account of its own.
		_accounts.With("ada", UserRole.Admin);
		Result r = await new DeleteUserHandler(_accounts, new FakeCurrentUser(FakeCurrentUser.ApiKey)).Handle(new DeleteUserCommand("ada"), Ct);
		Assert.Equal("PrintDeck needs at least one admin.", r.Error);
	}

	[Fact]
	public async Task Deletes_other_users()
	{
		_accounts.With("ada", UserRole.Admin).With("vic", UserRole.Viewer);
		Assert.True((await new DeleteUserHandler(_accounts, _me).Handle(new DeleteUserCommand("vic"), Ct)).IsSuccess);
		Assert.Equal("ada", Assert.Single(_accounts.Users).UserName);
	}

	[Fact]
	public async Task Changing_your_password_needs_the_current_one()
	{
		_accounts.With("ada", UserRole.Admin, "old-password");
		ChangeOwnPasswordHandler handler = new(_accounts, _me);

		Assert.Equal("Current password is wrong.", (await handler.Handle(new ChangeOwnPasswordCommand("guess", "new-password"), Ct)).Error);
		Assert.True((await handler.Handle(new ChangeOwnPasswordCommand("old-password", "new-password"), Ct)).IsSuccess);
		Assert.Equal("new-password", _accounts.PasswordOf("ada"));
	}

	[Fact]
	public async Task The_api_key_has_no_password_to_change()
	{
		Result r = await new ChangeOwnPasswordHandler(_accounts, new FakeCurrentUser(FakeCurrentUser.ApiKey))
			.Handle(new ChangeOwnPasswordCommand("a", "b"), Ct);
		Assert.Equal("Sign in with your own account to change its password.", r.Error);
	}

	[Fact]
	public async Task Create_and_reset_pass_through_to_the_accounts()
	{
		Assert.True((await new CreateUserHandler(_accounts).Handle(new CreateUserCommand(" vic ", "password123", UserRole.Viewer), Ct)).IsSuccess);
		Assert.True((await new ResetUserPasswordHandler(_accounts).Handle(new ResetUserPasswordCommand("vic", "fresh-password"), Ct)).IsSuccess);
		Assert.Equal("fresh-password", _accounts.PasswordOf("vic"));
	}
}
