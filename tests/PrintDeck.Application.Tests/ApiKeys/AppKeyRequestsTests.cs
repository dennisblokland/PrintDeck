using PrintDeck.Application.ApiKeys;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Users;
using PrintDeck.Domain.Users.Enums;

namespace PrintDeck.Application.Tests.ApiKeys;

public class AppKeyRequestsTests
{
	private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
	private readonly AppKeyRequests _requests;

	public AppKeyRequestsTests() => _requests = new AppKeyRequests(_time);

	private AppKeyRequest Add(string app = "Home Assistant", string? user = "ada")
	{
		Result<AppKeyRequest> r = _requests.Add(app, user);
		Assert.True(r.IsSuccess, r.Error);
		return r.Value!;
	}

	[Fact]
	public void A_request_waits_then_hands_out_its_key_once()
	{
		AppKeyRequest request = Add(" Home Assistant ", " ada ");
		Assert.Equal(("Home Assistant", "ada"), (request.App, request.User));
		Assert.Matches("^[0-9A-F]{32}$", request.Token);
		Assert.Equal([request], _requests.Pending());
		Assert.Equal((AppKeyRequestState.Pending, null), _requests.Poll(request.Token));

		Assert.True(_requests.Grant(request.Token, "SECRET"));

		Assert.Empty(_requests.Pending());
		Assert.Equal((AppKeyRequestState.Granted, "SECRET"), _requests.Poll(request.Token));
		Assert.Equal((AppKeyRequestState.Gone, null), _requests.Poll(request.Token));
		Assert.False(_requests.Grant(request.Token, "AGAIN"));
	}

	[Fact]
	public void A_denied_or_unknown_request_is_gone()
	{
		AppKeyRequest request = Add();

		Assert.True(_requests.Deny(request.Token));
		Assert.False(_requests.Deny(request.Token));
		Assert.Equal((AppKeyRequestState.Gone, null), _requests.Poll(request.Token));
		Assert.Equal((AppKeyRequestState.Gone, null), _requests.Poll("nope"));
	}

	[Fact]
	public void Requests_expire_and_so_do_keys_nobody_picks_up()
	{
		AppKeyRequest old = Add();
		_time.Advance(AppKeyRequests.PendingFor + TimeSpan.FromSeconds(1));
		Assert.Null(_requests.FindPending(old.Token));
		Assert.False(_requests.Grant(old.Token, "LATE"));

		AppKeyRequest fresh = Add();
		_requests.Grant(fresh.Token, "KEY");
		_time.Advance(AppKeyRequests.PickUpWithin + TimeSpan.FromSeconds(1));
		Assert.Equal((AppKeyRequestState.Gone, null), _requests.Poll(fresh.Token));
	}

	[Fact]
	public void Only_a_few_can_wait_at_once()
	{
		for (int i = 0; i < AppKeyRequests.MaxPending; i++) Add($"App {i}");

		Assert.Equal(AppKeyRequests.TooMany, _requests.Add("One more", null).Error);
		_time.Advance(AppKeyRequests.PendingFor + TimeSpan.FromSeconds(1));
		Assert.True(_requests.Add("One more", null).IsSuccess);
	}

	[Theory]
	[InlineData("", null)]
	[InlineData("   ", null)]
	[InlineData("bad\nname", null)]
	public void Refuses_a_nameless_app(string app, string? user)
	{
		Assert.Equal("Give the app a name of up to 64 characters.", _requests.Add(app, user).Error);
	}

	[Fact]
	public void A_request_without_a_user_names_none()
	{
		Assert.Null(Add(user: "  ").User);
	}
}
