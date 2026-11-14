using System.Text;

using PrintDeck.Application.Files;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Tests.Files;

public class GcodeToolpathParserTests
{
	private static Task<GcodeToolpath> Parse(string gcode) =>
		GcodeToolpathParser.ParseAsync(new MemoryStream(Encoding.UTF8.GetBytes(gcode)), TestContext.Current.CancellationToken);

	private static (float X, float Y) Point(GcodeToolpath t, int i) => (t.Xy[i * 2], t.Xy[i * 2 + 1]);

	public class Layers
	{
		[Fact]
		public async Task Splits_layers_on_z_and_ignores_travel_and_z_hops()
		{
			GcodeToolpath t = await Parse("""
				G28
				G90
				M82
				G1 Z0.2 F600
				G1 X10 Y10
				G1 X20 Y10 E1
				G1 X20 Y20 E2
				G1 Z0.6
				G1 X30 Y30
				G1 Z0.2
				G1 X40 Y30 E3
				G1 Z0.4
				G1 X10 Y10
				G1 X20 Y10 E4
				""");

			Assert.Equal(2, t.Layers.Count);
			Assert.Equal(0.2f, t.Layers[0].Z, 4);
			Assert.Equal(0.4f, t.Layers[1].Z, 4);
			// layer 0: travel to (10,10), two extrusions, travel to (30,30), extrusion
			Assert.Equal(5, t.Layers[0].Count);
			Assert.Equal([0, 7, 7, 0, 7], t.Kinds[..5].Select(k => (int) k));
			Assert.Equal((10f, 10f, 40f, 30f), t.Bounds);
		}

		[Fact]
		public async Task Follows_layer_markers_and_feature_types()
		{
			GcodeToolpath t = await Parse("""
				M83
				;LAYER_CHANGE
				;TYPE:Outer wall
				G1 Z0.2
				G1 X0 Y0
				G1 X10 Y0 E0.5
				;TYPE:Sparse infill
				G1 X10 Y10 E0.5
				;LAYER_CHANGE
				;TYPE:Support
				G1 X5 Y5 E0.5
				""");

			Assert.Equal(2, t.Layers.Count);
			Assert.Equal((byte) ToolpathKind.OuterWall, t.Kinds[1]);
			Assert.Equal((byte) ToolpathKind.Infill, t.Kinds[2]);
			Assert.Equal((byte) ToolpathKind.Support, t.Kinds[t.Layers[1].Start + 1]);
		}

		[Fact]
		public async Task Reads_bambu_feature_comments()
		{
			GcodeToolpath t = await Parse("M83\n; FEATURE: Inner wall\nG1 X0 Y0\nG1 X10 Y0 E1\n");
			Assert.Equal((byte) ToolpathKind.InnerWall, t.Kinds[1]);
		}

		[Fact]
		public async Task An_empty_file_has_no_layers()
		{
			GcodeToolpath t = await Parse("");
			Assert.Empty(t.Layers);
			Assert.Equal(0, t.PointCount);
			Assert.Equal((0f, 0f, 0f, 0f), t.Bounds);
			Assert.Equal(0, t.TotalBytes);
		}
	}

	public class Moves
	{
		[Fact]
		public async Task Extrusion_without_xy_movement_draws_nothing()
		{
			GcodeToolpath t = await Parse("M83\nG1 X5 Y5\nG1 E-1\nG1 E1\nG1 Z0.4 E0.1\n");
			Assert.Equal(0, t.PointCount);
		}

		[Fact]
		public async Task Follows_relative_positioning()
		{
			GcodeToolpath t = await Parse("G91\nG1 X5 Y5\nG1 X10 E1\n");
			Assert.Equal((5f, 5f), Point(t, 0));
			Assert.Equal((15f, 5f), Point(t, 1));
		}

		[Fact]
		public async Task G92_sets_the_position()
		{
			GcodeToolpath t = await Parse("G1 X50 Y50\nG92 X0 Y0 E0\nG1 X10 E1\n");
			Assert.Equal((0f, 0f), Point(t, 0));
			Assert.Equal((10f, 0f), Point(t, 1));
		}

		[Fact]
		public async Task Homing_only_resets_the_named_axes()
		{
			GcodeToolpath t = await Parse("G1 X50 Y50\nG28 X\nG1 X10 E1\n");
			Assert.Equal((0f, 50f), Point(t, 0));
		}

		[Fact]
		public async Task Offsets_match_how_the_print_session_counts_bytes()
		{
			const string gcode = "G1 X0 Y0\nG1 X5 Y0 E1\n";
			GcodeToolpath t = await Parse(gcode);
			uint afterSecondLine = (uint) Encoding.UTF8.GetByteCount(gcode);
			Assert.Equal([afterSecondLine, afterSecondLine], t.Offsets);
			Assert.Equal(afterSecondLine, t.TotalBytes);
		}

		[Fact]
		public async Task Interpolates_counter_clockwise_arcs()
		{
			GcodeToolpath t = await Parse("""
				G1 X10 Y0 Z0.2
				G3 X-10 Y0 I-10 J0 E5
				""");
			Assert.True(t.PointCount > 10);
			float maxY = Enumerable.Range(0, t.PointCount).Max(i => t.Xy[i * 2 + 1]);
			Assert.Equal(10f, maxY, 1); // counter-clockwise half circle passes through (0, 10)
		}

		[Fact]
		public async Task Interpolates_clockwise_arcs()
		{
			GcodeToolpath t = await Parse("G1 X10 Y0 Z0.2\nG2 X-10 Y0 I-10 J0 E5\n");
			Assert.Equal(-10f, t.Bounds.MinY, 1); // clockwise half circle passes through (0, -10)
			Assert.Equal((-10f, 0f), Point(t, t.PointCount - 1));
		}
	}

	[Theory]
	[InlineData("External perimeter", ToolpathKind.OuterWall)]
	[InlineData("WALL-OUTER", ToolpathKind.OuterWall)]
	[InlineData("Perimeter", ToolpathKind.InnerWall)]
	[InlineData("Inner wall", ToolpathKind.InnerWall)]
	[InlineData("FILL", ToolpathKind.Infill)]
	[InlineData("Internal infill", ToolpathKind.Infill)]
	[InlineData("Solid infill", ToolpathKind.SolidInfill)]
	[InlineData("Top surface", ToolpathKind.SolidInfill)]
	[InlineData("Bottom surface", ToolpathKind.SolidInfill)]
	[InlineData("Bridge infill", ToolpathKind.SolidInfill)]
	[InlineData("Ironing", ToolpathKind.SolidInfill)]
	[InlineData("SKIN", ToolpathKind.SolidInfill)]
	[InlineData("SUPPORT-INTERFACE", ToolpathKind.Support)]
	[InlineData("Skirt", ToolpathKind.SkirtBrim)]
	[InlineData("Brim", ToolpathKind.SkirtBrim)]
	[InlineData("Gap fill", ToolpathKind.Other)]
	[InlineData("", ToolpathKind.Other)]
	public void Maps_slicer_feature_names(string type, ToolpathKind kind) =>
		Assert.Equal(kind, GcodeToolpathParser.KindOf(type));
}
