using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Channels;

using PrintDeck.Application.Abstractions.Interfaces;

namespace PrintDeck.Application.Tests.TestDoubles;

/// <summary>
/// Printer link driven by the test: records every line written and answers each with
/// <see cref="Respond"/> (default: "ok"). <see cref="Emit"/> pushes unsolicited lines.
/// Acts as its own factory, so a reconnect reopens the same scripted printer.
/// </summary>
internal sealed partial class ScriptedTransport : IPrinterTransport, IPrinterTransportFactory
{
	private Channel<string> _out = Channel.CreateUnbounded<string>();
	private readonly List<string> _written = [];

	public Func<string, IEnumerable<string>> Respond { get; set; } = _ => ["ok"];
	public bool Disposed { get; private set; }

	[GeneratedRegex(@"^N\d+ (?<cmd>.*)\*\d+$")]
	private static partial Regex Numbered();

	/// <summary>Raw wire lines, e.g. "N3 G28*21".</summary>
	public IReadOnlyList<string> Written
	{
		get { lock (_written) return _written.ToList(); }
	}

	/// <summary>Commands without line number and checksum.</summary>
	public IReadOnlyList<string> Commands => Written.Select(CommandOf).ToList();

	public static string CommandOf(string wire) => Numbered().Match(wire) is { Success: true } m ? m.Groups["cmd"].Value : wire;

	public void Emit(string line) => _out.Writer.TryWrite(line);

	/// <summary>Simulates the printer going away (USB unplugged).</summary>
	public void Close() => _out.Writer.TryComplete();

	public IPrinterTransport Create(string port, int baudRate) => this;

	public Task OpenAsync(CancellationToken ct)
	{
		// Reopening after a disconnect behaves like a fresh port.
		if (Disposed || _out.Reader.Completion.IsCompleted) _out = Channel.CreateUnbounded<string>();
		Disposed = false;
		Emit("start");
		return Task.CompletedTask;
	}

	public Task WriteLineAsync(string line, CancellationToken ct)
	{
		lock (_written) _written.Add(line);
		foreach (string reply in Respond(line)) Emit(reply);
		return Task.CompletedTask;
	}

	public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken ct)
	{
		await foreach (string line in _out.Reader.ReadAllAsync(ct)) yield return line;
	}

	public ValueTask DisposeAsync()
	{
		Disposed = true;
		_out.Writer.TryComplete();
		return ValueTask.CompletedTask;
	}
}
