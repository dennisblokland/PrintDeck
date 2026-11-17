using PrintDeck.Application.Printer;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Tests.Printer;

public class PrintScriptsTests
{
	[Fact]
	public void The_default_cancel_script_turns_heaters_off_through_snippets()
	{
		PrintScripts scripts = PrintScripts.From(new HostSettings());

		Assert.Equal(["M104 S0", "M140 S0", "M107", "G91", "G1 Z10 F600", "G90", "M84"], scripts.Cancel);
		Assert.Empty(scripts.BeforePrint);
	}

	[Fact]
	public void A_script_that_does_not_expand_is_left_out()
	{
		HostSettings settings = new() { BeforePrintGcode = "G28\n{% snippet 'missing' %}", AfterPrintGcode = "M84" };

		PrintScripts scripts = PrintScripts.From(settings);

		Assert.Empty(scripts.BeforePrint);
		Assert.Equal(["M84"], scripts.AfterPrint);
	}
}
