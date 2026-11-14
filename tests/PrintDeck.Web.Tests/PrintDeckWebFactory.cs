using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Application.Abstractions.Interfaces;

namespace PrintDeck.Web.Tests;

/// <summary>The real app in memory, with uploads and settings in a throw-away directory.</summary>
public sealed class PrintDeckWebFactory : WebApplicationFactory<Program>
{
	private readonly string _data = Directory.CreateTempSubdirectory("printdeck-web-tests-").FullName;

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("Testing");
		builder.UseSetting("Storage:DataDirectory", _data);
		builder.UseSetting("Logging:LogLevel:Default", "Warning");
	}

	public async Task<string> ApiKeyAsync() =>
		(await Services.GetRequiredService<ISettingsStore>().GetAsync(CancellationToken.None)).ApiKey;

	public IGcodeFileStore Files => Services.GetRequiredService<IGcodeFileStore>();

	public override async ValueTask DisposeAsync()
	{
		await base.DisposeAsync();
		try { Directory.Delete(_data, recursive: true); } catch (IOException) { /* best effort */ }
	}
}
