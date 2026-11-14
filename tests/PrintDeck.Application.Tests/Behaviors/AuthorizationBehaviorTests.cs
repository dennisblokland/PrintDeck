using Mediator;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Behaviors;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;

namespace PrintDeck.Application.Tests.Behaviors;

public class AuthorizationBehaviorTests
{
	public sealed record PrinterCommand : ICommand<Result>;
	public sealed record ValueCommand : ICommand<Result<string>>;
	public sealed record StatusQuery : IQuery<string>;
	public sealed record SetupCommand : ICommand<Result>, IAllowAnonymous;
	public sealed record OwnPasswordCommand : ICommand<Result>, IAllowAnyUser;
	public sealed record ManageUsersCommand : ICommand<Result>, IAccountAdministration;
	public sealed record ListUsersQuery : IQuery<string>, IAccountAdministration;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static ValueTask<TResponse> Send<TMessage, TResponse>(TMessage message, CurrentUserInfo? user, TResponse handled)
		where TMessage : notnull, IMessage =>
		new AuthorizationBehavior<TMessage, TResponse>(new FakeCurrentUser(user))
			.Handle(message, (_, _) => ValueTask.FromResult(handled), Ct);

	private static Task<Result> Command<TMessage>(TMessage message, CurrentUserInfo? user) where TMessage : notnull, IMessage =>
		Send(message, user, Result.Success()).AsTask();

	public static TheoryData<string, CurrentUserInfo?, string?> Commands => new()
	{
		{ "printer, anonymous", null, AuthorizationBehavior<PrinterCommand, Result>.SignInRequired },
		{ "printer, viewer", FakeCurrentUser.Viewer, AuthorizationBehavior<PrinterCommand, Result>.AdminRequired },
		{ "printer, admin", FakeCurrentUser.Admin, null },
		{ "printer, api key", FakeCurrentUser.ApiKey, null },
	};

	[Theory]
	[MemberData(nameof(Commands))]
	public async Task Printer_commands_need_an_admin(string _, CurrentUserInfo? user, string? error)
	{
		Result result = await Command(new PrinterCommand(), user);
		Assert.Equal(error, result.Error);
		Assert.Equal(error is null, result.IsSuccess);
	}

	[Fact]
	public async Task Refused_commands_with_a_value_get_a_failed_typed_result()
	{
		Result<string> result = await Send(new ValueCommand(), FakeCurrentUser.Viewer, Result.Success("ran"));

		Assert.False(result.IsSuccess);
		Assert.Null(result.Value);
		Assert.Equal(AuthorizationBehavior<ValueCommand, Result<string>>.AdminRequired, result.Error);
	}

	[Fact]
	public async Task Queries_need_someone_signed_in()
	{
		Assert.Equal("ok", await Send(new StatusQuery(), FakeCurrentUser.Viewer, "ok"));
		await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await Send(new StatusQuery(), null, "ok"));
	}

	[Fact]
	public async Task Anonymous_messages_pass_without_asking_who_is_signed_in()
	{
		Assert.True((await Command(new SetupCommand(), null)).IsSuccess);
	}

	[Fact]
	public async Task Self_service_commands_are_open_to_viewers_but_not_to_anonymous()
	{
		Assert.True((await Command(new OwnPasswordCommand(), FakeCurrentUser.Viewer)).IsSuccess);
		Assert.False((await Command(new OwnPasswordCommand(), null)).IsSuccess);
	}

	[Fact]
	public async Task Account_administration_needs_a_signed_in_admin_not_the_api_key()
	{
		Assert.True((await Command(new ManageUsersCommand(), FakeCurrentUser.Admin)).IsSuccess);
		Assert.Equal(AuthorizationBehavior<ManageUsersCommand, Result>.AdminRequired, (await Command(new ManageUsersCommand(), FakeCurrentUser.Viewer)).Error);
		Assert.Equal(AuthorizationBehavior<ManageUsersCommand, Result>.AccountRequired, (await Command(new ManageUsersCommand(), FakeCurrentUser.ApiKey)).Error);
		await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await Send(new ListUsersQuery(), FakeCurrentUser.Viewer, "users"));
	}
}
