using PrintDeck.Domain.Protocol;

namespace PrintDeck.Domain.Tests.Protocol;

public class ActionCommandTests
{
	[Theory]
	[InlineData("//action:pause", "pause", "")]
	[InlineData("// action:cancel", "cancel", "")]
	[InlineData("  //action:prompt_begin Filament runout T0  ", "prompt_begin", "Filament runout T0")]
	[InlineData("//action:PAUSED", "paused", "")]
	[InlineData("//action:notification Heating done", "notification", "Heating done")]
	public void Parses_action_commands(string line, string name, string argument) =>
		Assert.Equal(new ActionCommand(name, argument), ActionCommand.TryParse(line));

	[Theory]
	[InlineData("ok")]
	[InlineData("echo:busy: paused for user")]
	[InlineData("// some comment")]
	[InlineData("//action:")]
	[InlineData("action:pause")]
	public void Ignores_other_lines(string line) => Assert.Null(ActionCommand.TryParse(line));
}
