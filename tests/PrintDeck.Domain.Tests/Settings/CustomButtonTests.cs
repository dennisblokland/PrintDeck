using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Domain.Tests.Settings;

public class CustomButtonTests
{
	private static readonly IReadOnlyList<GcodeSnippet> _snippets = [new("park", "G1 X0 Y200 F6000"), new("cool", "M104 S0\nM140 S0")];

	[Fact]
	public void Sends_the_lines_with_snippets_filled_in_and_skips_blank_lines_and_comments()
	{
		CustomButton button = new("Park", "G90\n\n; to the back\n{% snippet 'park' %}\n{% snippet \"cool\" %}");

		Result<IReadOnlyList<string>> r = button.Commands(_snippets);

		Assert.True(r.IsSuccess, r.Error);
		Assert.Equal(["G90", "G1 X0 Y200 F6000", "M104 S0", "M140 S0"], r.Value);
	}

	[Theory]
	[InlineData("M112", "M112 is an emergency stop: the printer halts mid-print.")]
	[InlineData("M110 N0", "M110 resets the line numbers PrintDeck is counting; the print stalls on resends.")]
	[InlineData("M28 test.gco", "M28 writes to the SD card: the rest of the file would be stored instead of printed.")]
	[InlineData("M997", "M997 starts a firmware update.")]
	[InlineData("M104 S{temperature}", "The slicer left {temperature} unfilled; the printer reads it as 0.")]
	[InlineData("{% snippet 'missing' %}", "Unknown snippet 'missing'.")]
	[InlineData("; only a comment\n\n", "There is no G-code to send.")]
	[InlineData("", "There is no G-code to send.")]
	public void Refuses_what_would_break_the_connection_or_send_nothing(string gcode, string error)
	{
		Assert.Equal(error, new CustomButton("X", gcode).Commands(_snippets).Error);
	}

	[Fact]
	public void Warnings_from_the_file_check_are_fine()
	{
		// Extruding without a hotend temperature is a warning for files; a button may rely on a preheated nozzle.
		Assert.True(new CustomButton("Purge", "G1 X10 E5 F300").Commands([]).IsSuccess);
	}

	[Theory]
	[InlineData("", "M84", null, "Every button needs a label.")]
	[InlineData("   ", "M84", null, "Every button needs a label.")]
	[InlineData("Way too long a label for a button on the Control page", "M84", null, "Button labels can be at most 40 characters.")]
	[InlineData("Off", "M84", "rocket", "Off: unknown icon 'rocket'.")]
	[InlineData("Off", "M112", null, "Off: M112 is an emergency stop: the printer halts mid-print.")]
	public void Problems_name_the_button(string label, string gcode, string? icon, string problem)
	{
		Assert.Equal(problem, new CustomButton(label, gcode, icon).Problem(_snippets));
	}

	[Fact]
	public void Idle_only_without_asking_by_default()
	{
		CustomButton button = new("Off", "M84");

		Assert.True(button.IdleOnly);
		Assert.False(button.Confirm);
		Assert.Null(button.Problem([]));
	}

	[Fact]
	public void The_mk3s_examples_are_valid_buttons()
	{
		Assert.All(CustomButton.PrusaExamples, b => Assert.Null(b.Problem([])));
	}

	[Fact]
	public void A_settings_file_without_buttons_gets_an_empty_list()
	{
		Assert.Empty(new HostSettings().CustomButtons);
		Assert.Empty((new HostSettings { CustomButtons = null! }).Normalized().CustomButtons);
	}
}
