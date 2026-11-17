using System.Net;

using PrintDeck.Domain.Users;

namespace PrintDeck.Domain.Tests.Users;

public class LoginThrottleTests
{
	private static readonly DateTimeOffset _t0 = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
	private readonly LoginThrottle _throttle = new(maxFailures: 3, window: TimeSpan.FromMinutes(15));

	private void Fail(string client, int times, DateTimeOffset at)
	{
		for (int i = 0; i < times; i++) _throttle.RecordFailure(client, at);
	}

	[Fact]
	public void Allows_attempts_below_the_limit()
	{
		Fail("1.2.3.4", 2, _t0);
		Assert.Null(_throttle.RetryAfter("1.2.3.4", _t0));
	}

	[Fact]
	public void Pauses_an_address_at_the_limit_until_the_oldest_failure_expires()
	{
		_throttle.RecordFailure("1.2.3.4", _t0);
		Fail("1.2.3.4", 2, _t0.AddMinutes(5));

		Assert.Equal(TimeSpan.FromMinutes(10), _throttle.RetryAfter("1.2.3.4", _t0.AddMinutes(5)));
		Assert.Equal(TimeSpan.FromMinutes(1), _throttle.RetryAfter("1.2.3.4", _t0.AddMinutes(14)));
		// The first failure has left the window; two remain, so one more try is allowed.
		Assert.Null(_throttle.RetryAfter("1.2.3.4", _t0.AddMinutes(15)));
		_throttle.RecordFailure("1.2.3.4", _t0.AddMinutes(15));
		Assert.Equal(TimeSpan.FromMinutes(5), _throttle.RetryAfter("1.2.3.4", _t0.AddMinutes(15)));
	}

	[Fact]
	public void Other_addresses_are_not_affected()
	{
		Fail("1.2.3.4", 3, _t0);
		Assert.NotNull(_throttle.RetryAfter("1.2.3.4", _t0));
		Assert.Null(_throttle.RetryAfter("5.6.7.8", _t0));
	}

	[Fact]
	public void Everything_is_forgotten_after_the_window()
	{
		Fail("1.2.3.4", 3, _t0);
		Assert.Null(_throttle.RetryAfter("1.2.3.4", _t0.AddMinutes(15)));
	}

	[Fact]
	public void Remembers_a_bounded_number_of_addresses_dropping_the_stalest()
	{
		LoginThrottle small = new(maxFailures: 1, window: TimeSpan.FromMinutes(15), maxClients: 2);
		small.RecordFailure("a", _t0);
		small.RecordFailure("b", _t0.AddMinutes(1));
		small.RecordFailure("c", _t0.AddMinutes(2));

		Assert.Null(small.RetryAfter("a", _t0.AddMinutes(2)));
		Assert.NotNull(small.RetryAfter("b", _t0.AddMinutes(2)));
		Assert.NotNull(small.RetryAfter("c", _t0.AddMinutes(2)));
	}

	[Theory]
	[InlineData("192.168.1.20", "192.168.1.20")]
	[InlineData("::ffff:192.168.1.20", "192.168.1.20")]
	[InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
	[InlineData("2001:db8:1:2::1", "2001:db8:1:2::/64")]
	[InlineData("::1", "::/64")]
	public void Ipv6_counts_per_64_block(string address, string key)
	{
		Assert.Equal(key, LoginThrottle.ClientKey(IPAddress.Parse(address)));
	}

	[Fact]
	public void No_address_is_one_shared_client()
	{
		Assert.Equal(LoginThrottle.UnknownClient, LoginThrottle.ClientKey(null));
	}
}
