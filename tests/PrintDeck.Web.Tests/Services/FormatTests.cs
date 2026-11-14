using PrintDeck.Web.Services;

namespace PrintDeck.Web.Tests.Services;

/// <summary>Runs under the invariant culture (see xunit.runner.json).</summary>
public class FormatTests
{
	[Theory]
	[InlineData(null, "—")]
	[InlineData(-5, "0s")]
	[InlineData(0, "0s")]
	[InlineData(59, "59s")]
	[InlineData(65, "1m 05s")]
	[InlineData(3599, "59m 59s")]
	[InlineData(3700, "1h 01m")]
	[InlineData(90000, "1d 1h")]
	public void Duration(int? seconds, string expected) =>
		Assert.Equal(expected, Format.Duration(seconds is { } s ? TimeSpan.FromSeconds(s) : null));

	[Theory]
	[InlineData(0, "0 B")]
	[InlineData(1023, "1023 B")]
	[InlineData(1536, "1.5 KB")]
	[InlineData(1024 * 1024, "1 MB")]
	[InlineData(5L * 1024 * 1024 * 1024 / 2, "2.5 GB")]
	public void Bytes(long bytes, string expected) => Assert.Equal(expected, Format.Bytes(bytes));

	[Theory]
	[InlineData(null, "—")]
	[InlineData(500.4, "500 mm")]
	[InlineData(1000.0, "1 m")]
	[InlineData(1234.5, "1.23 m")]
	public void Filament(double? mm, string expected) => Assert.Equal(expected, Format.Filament(mm));

	[Theory]
	[InlineData(200, "200.0")]
	[InlineData(21.24, "21.2")]
	[InlineData(-3.06, "-3.1")]
	public void Temp(double value, string expected) => Assert.Equal(expected, Format.Temp(value));

	[Theory]
	[InlineData(30, "just now")]
	[InlineData(5 * 60, "5 min ago")]
	[InlineData(3 * 3600, "3 h ago")]
	[InlineData(2 * 86400, "2 d ago")]
	public void Ago_is_relative_within_a_week(int secondsAgo, string expected)
	{
		DateTimeOffset now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
		Assert.Equal(expected, Format.Ago(now.AddSeconds(-secondsAgo), now));
	}

	[Fact]
	public void Ago_shows_the_date_after_a_week()
	{
		DateTimeOffset at = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero); // midday: same date in every time zone ±11 h
		Assert.Equal("15 Jan 2026", Format.Ago(at, at.AddDays(30)));
	}
}
