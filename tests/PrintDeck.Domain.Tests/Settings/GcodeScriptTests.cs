using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Domain.Tests.Settings;

public class GcodeScriptTests
{
	private static readonly GcodeSnippet[] _snippets =
	[
		new("disable_hotends", "M104 T0 S0\nM104 T1 S0"),
		new("disable_bed", "M140 S0"),
		new("all_off", "{% snippet 'disable_hotends' %}\n{% snippet \"disable_bed\" %}\nM107")
	];

	private static Result<IReadOnlyList<string>> Expand(string script, params GcodeSnippet[] snippets) =>
		GcodeScript.Expand(script, snippets.Length > 0 ? snippets : _snippets);

	[Fact]
	public void Keeps_plain_lines()
	{
		Assert.Equal(["G28", "; comment", "", "M84"], Expand("G28\r\n; comment\n\nM84").Value);
	}

	[Fact]
	public void Fills_in_snippets_with_either_quote()
	{
		Assert.Equal(["G91", "M104 T0 S0", "M104 T1 S0", "M140 S0", "G90"],
			Expand("G91\n{% snippet 'disable_hotends' %}\n  {%snippet \"disable_bed\"%}  \nG90").Value);
	}

	[Fact]
	public void Snippets_may_include_snippets()
	{
		Assert.Equal(["M104 T0 S0", "M104 T1 S0", "M140 S0", "M107", "M84"], Expand("{% snippet 'all_off' %}\nM84").Value);
	}

	[Fact]
	public void Names_ignore_case()
	{
		Assert.Equal(["M140 S0"], Expand("{% snippet 'Disable_Bed' %}").Value);
	}

	[Fact]
	public void Only_whole_lines_are_snippets()
	{
		Assert.Equal(["M117 {% snippet 'disable_bed' %}"], Expand("M117 {% snippet 'disable_bed' %}").Value);
	}

	[Fact]
	public void Rejects_an_unknown_snippet()
	{
		Assert.Equal("Unknown snippet 'disable_chamber'.", Expand("{% snippet 'disable_chamber' %}").Error);
	}

	[Fact]
	public void Rejects_a_snippet_that_includes_itself()
	{
		GcodeSnippet[] loop = [new("a", "G1\n{% snippet 'b' %}"), new("b", "{% snippet 'a' %}")];

		Assert.Equal("Snippet 'a' includes itself.", Expand("{% snippet 'a' %}", loop).Error);
	}

	[Theory]
	[InlineData("disable_hotends", true)]
	[InlineData("Park-2", true)]
	[InlineData("", false)]
	[InlineData("two words", false)]
	[InlineData("quote'", false)]
	public void Checks_snippet_names(string name, bool valid) => Assert.Equal(valid, GcodeScript.IsValidSnippetName(name));
}
