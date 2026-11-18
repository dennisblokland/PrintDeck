using PrintDeck.Domain.Settings;
using PrintDeck.Domain.Settings.Enums;
using PrintDeck.Domain.Terminal;

namespace PrintDeck.Domain.Tests.Settings;

public class HostSettingsTests
{
	private static readonly PrinterProfile _a = new() { Id = "a", Name = "A" };
	private static readonly PrinterProfile _b = new() { Id = "b", Name = "B" };

	[Fact]
	public void Defaults_have_one_active_profile()
	{
		HostSettings s = new();
		Assert.Single(s.Profiles);
		Assert.Equal(PrinterProfile.DefaultId, s.ActiveProfile.Id);
	}

	[Fact]
	public void Defaults_include_the_terminal_filters()
	{
		Assert.Equal(TerminalFilter.Defaults, new HostSettings().TerminalFilters);
	}

	[Fact]
	public void Normalized_keeps_an_emptied_filter_list_and_repairs_a_missing_one()
	{
		Assert.Empty(new HostSettings { TerminalFilters = [] }.Normalized().TerminalFilters);
		Assert.Empty(new HostSettings { TerminalFilters = null! }.Normalized().TerminalFilters);
	}

	[Fact]
	public void Normalized_resets_a_rotation_that_is_not_a_quarter_turn()
	{
		Assert.Equal(CameraRotation.None, new HostSettings { WebcamRotation = (CameraRotation)45 }.Normalized().WebcamRotation);
		Assert.Equal(CameraRotation.Half, new HostSettings { WebcamRotation = CameraRotation.Half }.Normalized().WebcamRotation);
	}

	[Fact]
	public void Active_profile_follows_the_id()
	{
		Assert.Equal(_b, new HostSettings { Profiles = [_a, _b], ActiveProfileId = "b" }.ActiveProfile);
	}

	[Fact]
	public void Stale_active_id_falls_back_to_the_first_profile()
	{
		HostSettings s = new() { Profiles = [_a, _b], ActiveProfileId = "gone" };
		Assert.Equal(_a, s.ActiveProfile);
		Assert.Equal("a", s.Normalized().ActiveProfileId);
	}

	[Fact]
	public void Normalized_adds_a_profile_when_none_are_left()
	{
		HostSettings s = new HostSettings { Profiles = [], ActiveProfileId = "x" }.Normalized();
		Assert.Single(s.Profiles);
		Assert.Equal(s.Profiles[0].Id, s.ActiveProfileId);
	}

	[Fact]
	public void Normalized_gives_blank_and_repeated_ids_new_ones()
	{
		HostSettings s = new HostSettings { Profiles = [_a, _a with { Name = "A2" }, _b with { Id = "" }], ActiveProfileId = "a" }.Normalized();

		Assert.Equal(3, s.Profiles.Select(p => p.Id).Distinct().Count());
		Assert.All(s.Profiles, p => Assert.False(string.IsNullOrWhiteSpace(p.Id)));
		Assert.Equal("A", s.ActiveProfile.Name);
	}
}
