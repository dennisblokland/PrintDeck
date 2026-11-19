using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.ApiKeys;
using PrintDeck.Application.Features.ApiKeys.Commands;
using PrintDeck.Application.Features.ApiKeys.Dtos;
using PrintDeck.Application.Features.ApiKeys.Handlers;
using PrintDeck.Application.Features.ApiKeys.Queries;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Tests.Features.ApiKeys;

public class AppKeyRequestHandlerTests
{
	private readonly AppKeyRequests _requests = new(TimeProvider.System);
	private readonly FakeApiKeys _keys = new();
	private readonly FakeCurrentUser _me = new(FakeCurrentUser.Admin);
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private async Task<AppKeyRequest> Ask(string app = "Home Assistant", string? user = "vic")
	{
		Result<AppKeyRequest> r = await new RequestAppKeyHandler(_requests, _events.Hub, NullLogger<RequestAppKeyHandler>.Instance)
			.Handle(new RequestAppKeyCommand(app, user), Ct);
		Assert.True(r.IsSuccess, r.Error);
		return r.Value!;
	}

	private ValueTask<Result> Approve(string token) =>
		new ApproveAppKeyRequestHandler(_requests, _keys, _me, _events.Hub, NullLogger<ApproveAppKeyRequestHandler>.Instance)
			.Handle(new ApproveAppKeyRequestCommand(token), Ct);

	private ValueTask<AppKeyPoll> Poll(string token) => new PollAppKeyRequestHandler(_requests).Handle(new PollAppKeyRequestQuery(token), Ct);

	[Fact]
	public async Task Asking_shows_the_request_to_admins()
	{
		AppKeyRequest request = await Ask();

		Assert.Contains(PrinterChange.Settings, _events.Published);
		Assert.Equal([request], await new ListAppKeyRequestsHandler(_requests).Handle(new ListAppKeyRequestsQuery(), Ct));
	}

	[Fact]
	public async Task Allowing_makes_a_key_for_the_account_the_app_named()
	{
		AppKeyRequest request = await Ask(user: "vic");

		Assert.True((await Approve(request.Token)).IsSuccess);

		ApiKeyInfo key = Assert.Single(_keys.Keys);
		Assert.Equal(("Home Assistant", "vic"), (key.Name, key.OwnerName));
		AppKeyPoll poll = await Poll(request.Token);
		Assert.Equal(AppKeyRequestState.Granted, poll.State);
		Assert.Equal(key.Hint, ApiKeySecret.Hint(poll.Key!));
	}

	[Fact]
	public async Task Without_a_user_the_key_acts_as_the_admin_allowing_it()
	{
		AppKeyRequest request = await Ask(user: null);

		await Approve(request.Token);

		Assert.Equal("ada", Assert.Single(_keys.Keys).OwnerName);
	}

	[Fact]
	public async Task A_second_key_for_the_same_app_gets_a_number()
	{
		await Approve((await Ask()).Token);
		await Approve((await Ask()).Token);

		Assert.Equal(["Home Assistant", "Home Assistant (2)"], _keys.Keys.Select(k => k.Name));
	}

	[Fact]
	public async Task An_unknown_account_makes_no_key()
	{
		AppKeyRequest request = await Ask(user: "nobody");

		Assert.Equal("There is no account named nobody. Ask the app to use an existing one.", (await Approve(request.Token)).Error);
		Assert.Empty(_keys.Keys);
		Assert.Equal(AppKeyRequestState.Pending, (await Poll(request.Token)).State);
	}

	[Fact]
	public async Task An_api_key_cannot_allow_requests()
	{
		AppKeyRequest request = await Ask();
		_me.User = FakeCurrentUser.ApiKey;

		Assert.Equal("Sign in with an admin account to allow app keys.", (await Approve(request.Token)).Error);
		Assert.Empty(_keys.Keys);
	}

	[Fact]
	public async Task Denying_ends_the_request()
	{
		AppKeyRequest request = await Ask();

		Assert.True((await new DenyAppKeyRequestHandler(_requests, _events.Hub).Handle(new DenyAppKeyRequestCommand(request.Token), Ct)).IsSuccess);

		Assert.Equal(AppKeyRequestState.Gone, (await Poll(request.Token)).State);
		Assert.Equal(ApproveAppKeyRequestHandler.Gone, (await Approve(request.Token)).Error);
		Assert.Equal(ApproveAppKeyRequestHandler.Gone,
			(await new DenyAppKeyRequestHandler(_requests, _events.Hub).Handle(new DenyAppKeyRequestCommand(request.Token), Ct)).Error);
	}
}
