using PrintDeck.Domain.Health;
using PrintDeck.Domain.Health.Enums;

namespace PrintDeck.Domain.Tests.Health;

public class PiThrottlingTests
{
	[Theory]
	[InlineData("throttled=0x0", 0)]
	[InlineData("throttled=0x50005\n", 0x50005)]
	[InlineData("50000", 0x50000)]
	[InlineData(" 0x1 ", 1)]
	public void Parses_vcgencmd_and_sysfs_output(string text, int flags) => Assert.Equal(flags, PiThrottling.Parse(text)!.Flags);

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	[InlineData("VCHI initialization failed")]
	public void Unreadable_output_is_null(string? text) => Assert.Null(PiThrottling.Parse(text));

	[Fact]
	public void All_clear_has_no_warnings() => Assert.Empty(new PiThrottling(0).Check());

	[Fact]
	public void Under_voltage_now_is_an_error_and_wins_over_since_boot()
	{
		HealthWarning w = Assert.Single(new PiThrottling(0x10001).Check());
		Assert.Equal(("pi-undervoltage", HealthSeverity.Error, "The Raspberry Pi has too little power"), (w.Id, w.Severity, w.Title));
	}

	[Fact]
	public void Under_voltage_since_boot_is_a_warning()
	{
		HealthWarning w = Assert.Single(new PiThrottling(0x10000).Check());
		Assert.Equal((HealthSeverity.Warning, "The Raspberry Pi had too little power"), (w.Severity, w.Title));
	}

	[Theory]
	[InlineData(0x4, "The Raspberry Pi is slowed down")]
	[InlineData(0x8, "The Raspberry Pi is slowed down")]
	[InlineData(0x20000, "The Raspberry Pi was slowed down")]
	[InlineData(0x80000, "The Raspberry Pi was slowed down")]
	public void Throttling_is_a_warning(int flags, string title)
	{
		HealthWarning w = Assert.Single(new PiThrottling(flags).Check());
		Assert.Equal(("pi-throttled", HealthSeverity.Warning, title), (w.Id, w.Severity, w.Title));
	}

	[Fact]
	public void The_classic_power_problem_gives_both_warnings()
	{
		Assert.Equal(["pi-undervoltage", "pi-throttled"], new PiThrottling(0x50005).Check().Select(w => w.Id));
	}
}
