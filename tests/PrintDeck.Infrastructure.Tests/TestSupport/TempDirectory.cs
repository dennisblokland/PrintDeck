namespace PrintDeck.Infrastructure.Tests.TestSupport;

/// <summary>Scratch directory deleted when the test ends.</summary>
internal sealed class TempDirectory : IDisposable
{
	public string Path { get; } = Directory.CreateTempSubdirectory("printdeck-tests-").FullName;

	public void Dispose()
	{
		try { Directory.Delete(Path, recursive: true); } catch (IOException) { /* best effort */ }
	}
}
