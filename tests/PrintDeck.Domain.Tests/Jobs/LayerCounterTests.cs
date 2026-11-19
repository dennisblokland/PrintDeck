using PrintDeck.Domain.Jobs;

namespace PrintDeck.Domain.Tests.Jobs;

public class LayerCounterTests
{
	[Fact]
	public void Cura_and_orca_layers_count_from_zero()
	{
		LayerCounter layers = new();

		Assert.Equal(0, layers.Layer);
		Assert.Equal(1, layers.OnLine(";LAYER:0"));
		Assert.Null(layers.OnLine("G1 X10 Y10"));
		Assert.Equal(2, layers.OnLine("  ; LAYER:1"));
		Assert.Equal(2, layers.Layer);
	}

	[Fact]
	public void Prusaslicer_layer_changes_are_counted()
	{
		LayerCounter layers = new();

		Assert.Equal(1, layers.OnLine(";LAYER_CHANGE"));
		Assert.Null(layers.OnLine(";Z:0.2"));
		Assert.Equal(2, layers.OnLine(";LAYER_CHANGE"));
	}

	[Fact]
	public void Numbered_layers_win_over_layer_change_markers()
	{
		LayerCounter layers = new();

		layers.OnLine(";LAYER:4");
		Assert.Null(layers.OnLine(";LAYER_CHANGE"));
		Assert.Null(layers.OnLine(";LAYER:4")); // the same layer again
		Assert.Equal(5, layers.Layer);
	}

	[Fact]
	public void Other_comments_and_commands_are_not_layers()
	{
		LayerCounter layers = new();

		Assert.Null(layers.OnLine(";LAYER:abc"));
		Assert.Null(layers.OnLine("M117 LAYER:3"));
		Assert.Null(layers.OnLine(";TYPE:WALL-OUTER"));
		Assert.Equal(0, layers.Layer);
	}
}
