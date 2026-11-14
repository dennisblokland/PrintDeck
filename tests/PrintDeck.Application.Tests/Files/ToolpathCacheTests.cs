using System.Text;

using PrintDeck.Application.Files;
using PrintDeck.Domain.Files;

namespace PrintDeck.Application.Tests.Files;

public class ToolpathCacheTests
{
	private static readonly DateTimeOffset _t0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
	private const string _gcode = "G1 X0 Y0\nG1 X10 Y0 E1\n";

	private readonly ToolpathCache _cache = new();
	private readonly Dictionary<string, int> _opens = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static GcodeFile File(string name, DateTimeOffset? uploadedAt = null) =>
		new(name, Encoding.UTF8.GetByteCount(_gcode), uploadedAt ?? _t0, null);

	private Task<GcodeToolpath> Get(GcodeFile file) => _cache.GetAsync(file, () =>
	{
		_opens[file.Name] = _opens.GetValueOrDefault(file.Name) + 1;
		return new MemoryStream(Encoding.UTF8.GetBytes(_gcode));
	}, Ct);

	[Fact]
	public async Task Parses_a_file_once()
	{
		GcodeToolpath first = await Get(File("a.gcode"));
		GcodeToolpath second = await Get(File("a.gcode"));

		Assert.Same(first, second);
		Assert.Equal(1, _opens["a.gcode"]);
	}

	[Fact]
	public async Task Reparses_a_new_upload_with_the_same_name()
	{
		await Get(File("a.gcode"));
		await Get(File("a.gcode", _t0.AddMinutes(1)));
		Assert.Equal(2, _opens["a.gcode"]);
	}

	[Fact]
	public async Task Invalidate_forgets_a_file()
	{
		await Get(File("a.gcode"));
		await Get(File("b.gcode"));
		_cache.Invalidate("a.gcode");

		await Get(File("a.gcode"));
		await Get(File("b.gcode"));

		Assert.Equal(2, _opens["a.gcode"]);
		Assert.Equal(1, _opens["b.gcode"]);
	}

	[Fact]
	public async Task Evicts_the_least_recently_used_file()
	{
		foreach (string name in new[] { "a", "b", "c" }) await Get(File(name));
		await Get(File("a")); // a is now most recent; b is oldest
		await Get(File("d")); // evicts b

		await Get(File("a"));
		await Get(File("b"));

		Assert.Equal(1, _opens["a"]);
		Assert.Equal(2, _opens["b"]);
	}

	[Fact]
	public async Task Retries_after_a_failed_parse()
	{
		GcodeFile file = File("a.gcode");
		await Assert.ThrowsAsync<IOException>(() => _cache.GetAsync(file, () => throw new IOException("disk gone"), Ct));

		GcodeToolpath t = await Get(file);
		Assert.Equal(2, t.PointCount);
	}

	[Fact]
	public async Task A_cancelled_caller_does_not_cancel_the_shared_parse()
	{
		TaskCompletionSource gate = new();
		GcodeFile file = File("slow.gcode");
		using CancellationTokenSource cts = new();

		Task<GcodeToolpath> cancelled = _cache.GetAsync(file, () =>
		{
			gate.Task.Wait(Ct);
			return new MemoryStream(Encoding.UTF8.GetBytes(_gcode));
		}, cts.Token);
		await cts.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);

		gate.SetResult();
		GcodeToolpath t = await _cache.GetAsync(file, () => throw new InvalidOperationException("should reuse"), Ct);
		Assert.Equal(2, t.PointCount);
	}
}
