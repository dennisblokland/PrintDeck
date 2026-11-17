using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.ApiKeys.Commands;
using PrintDeck.Application.Features.ApiKeys.Handlers;
using PrintDeck.Application.Features.ApiKeys.Queries;
using PrintDeck.Application.Features.Settings.Commands;
using PrintDeck.Application.Features.Settings.Handlers;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Tests.Features.ApiKeys;

public class ApiKeyHandlerTests
{
	private readonly FakeApiKeys _keys = new();
	private readonly FakeCurrentUser _me = new(FakeCurrentUser.Admin);
	private readonly FakeSettingsStore _settings = new(new HostSettings { ApiKey = "SHARED" });
	private readonly (PrinterEventHub Hub, List<PrinterChange> Published) _events = RecordingEventHub.Create();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private ValueTask<Result<string>> Create(string name) =>
		new CreateApiKeyHandler(_keys, _me, _events.Hub, NullLogger<CreateApiKeyHandler>.Instance).Handle(new CreateApiKeyCommand(name), Ct);

	private ValueTask<Result> Revoke(string id) =>
		new RevokeApiKeyHandler(_keys, _me, _events.Hub, NullLogger<RevokeApiKeyHandler>.Instance).Handle(new RevokeApiKeyCommand(id), Ct);

	[Fact]
	public async Task Create_returns_the_key_once_and_lists_it_as_the_signed_in_admin()
	{
		Result<string> r = await Create("  OrcaSlicer ");

		Assert.True(r.IsSuccess);
		ApiKeyInfo listed = Assert.Single(await new ListApiKeysHandler(_keys).Handle(new ListApiKeysQuery(), Ct));
		Assert.Equal("OrcaSlicer", listed.Name);
		Assert.Equal("ada", listed.OwnerName);
		Assert.Equal(ApiKeySecret.Hint(r.Value!), listed.Hint);
		Assert.Equal([PrinterChange.Settings], _events.Published);
	}

	[Theory]
	[InlineData("", "Give the key a name of up to 64 characters.")]
	[InlineData("orcaslicer", "A key with that name already exists.")]
	public async Task Create_refuses_bad_names(string name, string error)
	{
		await Create("OrcaSlicer");

		Assert.Equal(error, (await Create(name)).Error);
		Assert.Single(_keys.Keys);
	}

	[Fact]
	public async Task Create_needs_a_real_account()
	{
		_me.User = FakeCurrentUser.ApiKey;
		Assert.Equal("Sign in with an admin account to make keys.", (await Create("OrcaSlicer")).Error);

		_me.User = new CurrentUserInfo("ghost", UserRole.Admin);
		Assert.Equal("No user named ghost.", (await Create("OrcaSlicer")).Error);
		Assert.Empty(_keys.Keys);
	}

	[Fact]
	public async Task Revoke_removes_the_key()
	{
		await Create("OrcaSlicer");
		string id = Assert.Single(_keys.Keys).Id;

		Assert.True((await Revoke(id)).IsSuccess);
		Assert.Empty(_keys.Keys);
		Assert.Equal("That key no longer exists.", (await Revoke(id)).Error);
	}

	[Fact]
	public async Task Turning_the_shared_key_off_also_replaces_it()
	{
		Result r = await new DisableSharedApiKeyHandler(_settings, _events.Hub).Handle(new DisableSharedApiKeyCommand(), Ct);

		Assert.True(r.IsSuccess);
		Assert.False(_settings.Settings.SharedApiKeyEnabled);
		Assert.NotEqual("SHARED", _settings.Settings.ApiKey);
		Assert.Equal([PrinterChange.Settings], _events.Published);
	}

	[Fact]
	public async Task A_new_shared_key_turns_it_back_on()
	{
		await new DisableSharedApiKeyHandler(_settings, _events.Hub).Handle(new DisableSharedApiKeyCommand(), Ct);

		Result<string> r = await new RegenerateApiKeyHandler(_settings, _events.Hub).Handle(new RegenerateApiKeyCommand(), Ct);

		Assert.True(_settings.Settings.SharedApiKeyEnabled);
		Assert.Equal(r.Value, _settings.Settings.ApiKey);
	}
}
