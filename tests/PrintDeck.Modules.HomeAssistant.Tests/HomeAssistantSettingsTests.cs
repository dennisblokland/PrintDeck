namespace PrintDeck.Modules.HomeAssistant.Tests;

public sealed class HomeAssistantSettingsTests
{
	[Theory]
	[InlineData("", "", 3, null)]
	[InlineData("VIRTUAL", "light.printer", 0, null)]
	[InlineData("virtual", "switch.plug", 120, null)]
	[InlineData("http://ha.local:8123", "light.printer", 3, null)]
	[InlineData("ha.local", "", 3, "The Home Assistant URL must start with http:// or https://, or be VIRTUAL.")]
	[InlineData("ftp://ha.local", "", 3, "The Home Assistant URL must start with http:// or https://, or be VIRTUAL.")]
	[InlineData("VIRTUAL", "sensor.temp", 3, "The light must be a light.* or switch.* entity.")]
	[InlineData("VIRTUAL", "light.", 3, "The light must be a light.* or switch.* entity.")]
	[InlineData("VIRTUAL", "light.printer", -1, "Switch the light off 0 to 120 minutes after.")]
	[InlineData("VIRTUAL", "light.printer", 121, "Switch the light off 0 to 120 minutes after.")]
	public void Says_what_is_wrong(string url, string light, int delay, string? problem)
	{
		HomeAssistantSettings s = new() { Url = url, LightEntity = light, LightOffDelayMinutes = delay };

		Assert.Equal(problem, s.Problem());
	}

	[Fact]
	public void Trims_the_text_fields()
	{
		HomeAssistantSettings s = new HomeAssistantSettings { Url = " VIRTUAL ", Token = " t ", LightEntity = " light.a ", LightWhilePrinting = true }.Trimmed();

		Assert.Equal(("VIRTUAL", "t", "light.a", true), (s.Url, s.Token, s.LightEntity, s.LightWhilePrinting));
		Assert.True(s.IsVirtual);
	}
}
