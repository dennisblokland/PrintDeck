using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Abstractions.Interfaces;

namespace PrintDeck.Infrastructure.Serial;

public sealed class SerialTransport(string portName, int baudRate) : IPrinterTransport
{
	private readonly SerialPort _port = new(portName, baudRate)
	{
		NewLine = "\n",
		Encoding = Encoding.ASCII,
		ReadTimeout = 500,
		WriteTimeout = 5000,
		DtrEnable = true,
		RtsEnable = true
	};

	private readonly Channel<string> _lines = Channel.CreateUnbounded<string>(new() { SingleReader = true });
	private readonly SemaphoreSlim _writeLock = new(1, 1);
	private Thread? _readThread;
	private volatile bool _closing;

	public Task OpenAsync(CancellationToken ct)
	{
		_port.Open();
		_port.DiscardInBuffer();
		_readThread = new Thread(ReadLoop) { IsBackground = true, Name = $"serial-{portName}" };
		_readThread.Start();
		return Task.CompletedTask;
	}

	private void ReadLoop()
	{
		try
		{
			while (!_closing && _port.IsOpen)
			{
				try { _lines.Writer.TryWrite(_port.ReadLine().TrimEnd('\r')); }
				catch (TimeoutException) { }
			}
			_lines.Writer.TryComplete();
		}
		catch (Exception ex)
		{
			_lines.Writer.TryComplete(_closing ? null : ex);
		}
	}

	public async Task WriteLineAsync(string line, CancellationToken ct)
	{
		await _writeLock.WaitAsync(ct);
		try { _port.Write(line + "\n"); }
		finally { _writeLock.Release(); }
	}

	public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken ct)
	{
		await foreach (string line in _lines.Reader.ReadAllAsync(ct))
			yield return line;
	}

	public ValueTask DisposeAsync()
	{
		_closing = true;
		try { if (_port.IsOpen) _port.Close(); } catch (IOException) { }
		_readThread?.Join(TimeSpan.FromSeconds(2));
		_port.Dispose();
		return ValueTask.CompletedTask;
	}
}
