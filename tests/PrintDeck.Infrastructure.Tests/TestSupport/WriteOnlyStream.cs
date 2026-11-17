namespace PrintDeck.Infrastructure.Tests.TestSupport;

/// <summary>A forward-only stream like an HTTP response body: no seeking, no reading, async writes only.</summary>
internal sealed class WriteOnlyStream : Stream
{
	private readonly MemoryStream _inner = new();

	public byte[] ToArray() => _inner.ToArray();

	public override bool CanRead => false;
	public override bool CanSeek => false;
	public override bool CanWrite => true;
	public override long Length => throw new NotSupportedException();
	public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

	public override void Flush() { }
	public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
	public override void SetLength(long value) => throw new NotSupportedException();

	// Kestrel refuses synchronous writes; so does this.
	public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous writes are not allowed.");

	public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
		_inner.WriteAsync(buffer, cancellationToken);

	public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
		_inner.WriteAsync(buffer, offset, count, cancellationToken);
}
