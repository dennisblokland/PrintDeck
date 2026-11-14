using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Channels;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;

namespace PrintDeck.Infrastructure.Virtual;

/// <summary>
/// In-process Marlin imitation for development without hardware:
/// heaters with thermal lag, M109/M190 waits, auto-reporting, checksum validation and moves that take time.
/// </summary>
public sealed partial class VirtualPrinterTransport(TimeProvider time, double speedFactor = 1) : IPrinterTransport
{
	public const string PortName = "VIRTUAL";

	private readonly Channel<string> _out = Channel.CreateUnbounded<string>();
	private readonly Channel<string> _in = Channel.CreateUnbounded<string>(new() { SingleReader = true });
	private readonly CancellationTokenSource _cts = new();
	private readonly Heater[] _heaters = [new("T0", 22), new("B", 22)];
	private Task _loops = Task.CompletedTask;
	private long _expectedLine;
	private TimeSpan _autoReport = TimeSpan.Zero;
	private double _x, _y, _z, _feed = 3000;
	private bool _relative;

	[GeneratedRegex(@"^N(?<n>\d+)\s+(?<cmd>.*)\*(?<cs>\d+)$")]
	private static partial Regex Numbered();

	public Task OpenAsync(CancellationToken ct)
	{
		_out.Writer.TryWrite("start");
		_out.Writer.TryWrite("echo: PrintDeck virtual printer");
		_loops = Task.WhenAll(Task.Run(() => ProcessAsync(_cts.Token)), Task.Run(() => PhysicsAsync(_cts.Token)));
		return Task.CompletedTask;
	}

	public Task WriteLineAsync(string line, CancellationToken ct) => _in.Writer.WriteAsync(line, ct).AsTask();

	public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken ct)
	{
		await foreach (string line in _out.Reader.ReadAllAsync(ct)) yield return line;
	}

	private void Emit(string line) => _out.Writer.TryWrite(line);
	private string TempReport => string.Join(' ', _heaters.Select(h => h.Report)).Replace("T0:", "T:") + " @:0 B@:0";

	private async Task PhysicsAsync(CancellationToken ct)
	{
		DateTimeOffset lastReport = time.GetUtcNow();
		using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(250), time);
		while (await timer.WaitForNextTickAsync(ct))
		{
			foreach (Heater h in _heaters)
			{
				double goal = h.Target > 0 ? h.Target : h.Ambient;
				double rate = h.Target > h.Actual ? 0.06 : 0.02;
				h.Actual += (goal - h.Actual) * rate + (Random.Shared.NextDouble() - 0.5) * 0.3;
			}
			if (_autoReport > TimeSpan.Zero && time.GetUtcNow() - lastReport >= _autoReport)
			{
				Emit(" " + TempReport);
				lastReport = time.GetUtcNow();
			}
		}
	}

	private async Task ProcessAsync(CancellationToken ct)
	{
		await foreach (string wire in _in.Reader.ReadAllAsync(ct))
		{
			string command = wire.Trim();
			Match m = Numbered().Match(command);
			if (m.Success)
			{
				long n = long.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture);
				string body = command[..command.LastIndexOf('*')];
				byte cs = 0;
				foreach (char c in body) cs ^= (byte) c;

				if (cs.ToString(CultureInfo.InvariantCulture) != m.Groups["cs"].Value)
				{
					Emit($"Error:checksum mismatch, Last Line: {_expectedLine - 1}");
					Emit($"Resend: {_expectedLine}");
					Emit("ok");
					continue;
				}
				command = m.Groups["cmd"].Value;
				if (command.StartsWith("M110", StringComparison.Ordinal)) _expectedLine = n;
				else if (n != _expectedLine)
				{
					Emit($"Error:Line Number is not Last Line Number+1, Last Line: {_expectedLine - 1}");
					Emit($"Resend: {_expectedLine}");
					Emit("ok");
					continue;
				}
				_expectedLine = n + 1;
			}

			await ExecuteAsync(command, ct);
		}
	}

	private async Task ExecuteAsync(string command, CancellationToken ct)
	{
		string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		string code = parts.Length > 0 ? parts[0].ToUpperInvariant() : "";
		double? Arg(char letter) => parts.Skip(1).Where(p => char.ToUpperInvariant(p[0]) == letter)
			.Select(p => double.TryParse(p.AsSpan(1), CultureInfo.InvariantCulture, out double v) ? v : (double?) null)
			.FirstOrDefault();

		switch (code)
		{
			case "M105":
				Emit("ok " + TempReport);
				return;
			case "M115":
				Emit("FIRMWARE_NAME:Marlin 2.1.2 (PrintDeck Virtual) SOURCE_CODE_URL:github.com/MarlinFirmware/Marlin PROTOCOL_VERSION:1.0 MACHINE_TYPE:Virtual EXTRUDER_COUNT:1");
				Emit("Cap:AUTOREPORT_TEMP:1");
				break;
			case "M155":
				_autoReport = TimeSpan.FromSeconds(Arg('S') ?? 0);
				break;
			case "M104" or "M109":
				_heaters[0].Target = Arg('S') ?? 0;
				if (code == "M109") await WaitForAsync(_heaters[0], ct);
				break;
			case "M140" or "M190":
				_heaters[1].Target = Arg('S') ?? 0;
				if (code == "M190") await WaitForAsync(_heaters[1], ct);
				break;
			case "M114":
				Emit(FormattableString.Invariant($"X:{_x:0.00} Y:{_y:0.00} Z:{_z:0.00} E:0.00 Count X:0 Y:0 Z:0"));
				break;
			case "G28":
				await Task.Delay(TimeSpan.FromSeconds(1.5), time, ct);
				(_x, _y, _z) = (0, 0, 0);
				break;
			case "G90": _relative = false; break;
			case "G91": _relative = true; break;
			case "G0" or "G1":
				(double nx, double ny, double nz) = (_x, _y, _z);
				if (Arg('X') is { } x) nx = _relative ? _x + x : x;
				if (Arg('Y') is { } y) ny = _relative ? _y + y : y;
				if (Arg('Z') is { } z) nz = _relative ? _z + z : z;
				if (Arg('F') is { } f && f > 0) _feed = f;
				double dist = Math.Sqrt(Math.Pow(nx - _x, 2) + Math.Pow(ny - _y, 2) + Math.Pow(nz - _z, 2));
				double seconds = dist / (_feed / 60.0) / speedFactor;
				if (seconds > 0.001) await Task.Delay(TimeSpan.FromSeconds(Math.Min(seconds, 5)), time, ct);
				(_x, _y, _z) = (nx, ny, nz);
				break;
			case "M112":
				Emit("Error:Printer halted. kill() called!");
				return;
		}
		Emit("ok");
	}

	private async Task WaitForAsync(Heater heater, CancellationToken ct)
	{
		while (Math.Abs(heater.Actual - heater.Target) > 1.5 && heater.Target > 0)
		{
			Emit(FormattableString.Invariant($" {TempReport} W:?"));
			await Task.Delay(TimeSpan.FromSeconds(1), time, ct);
		}
	}

	public async ValueTask DisposeAsync()
	{
		await _cts.CancelAsync();
		_in.Writer.TryComplete();
		_out.Writer.TryComplete();
		try { await _loops; } catch (OperationCanceledException) { }
		_cts.Dispose();
	}
}
