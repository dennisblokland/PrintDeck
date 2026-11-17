using System.Net;
using System.Text;

using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Printer.Enums;

namespace PrintDeck.Web.Tests.Api;

/// <summary>"Upload and print" from a slicer while PrintDeck is not connected yet: it connects to the last port first.</summary>
public sealed class SlicerApiPrintAfterUploadTests : IAsyncDisposable
{
	private readonly PrintDeckWebFactory _app = new();
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public async ValueTask DisposeAsync() => await _app.DisposeAsync();

	[Fact]
	public async Task Connects_to_the_last_port_and_starts_the_print()
	{
		HttpClient client = _app.CreateClient();
		ISettingsStore settings = _app.Services.GetRequiredService<ISettingsStore>();
		await settings.UpdateAsync(s => s with { LastPort = "VIRTUAL", DefaultBaudRate = 115200 }, Ct);
		IPrinterSession session = _app.Services.GetRequiredService<IPrinterSession>();
		Assert.Equal(PrinterState.Offline, session.State);

		HttpRequestMessage request = new(HttpMethod.Post, "/api/files/local");
		request.Headers.Add("X-Api-Key", await _app.ApiKeyAsync());
		string gcode = string.Concat(Enumerable.Repeat("G1 X10 F600\nG1 X20 F600\n", 200));
		request.Content = new MultipartFormDataContent
		{
			{ new ByteArrayContent(Encoding.UTF8.GetBytes(gcode)), "file", "slicer-print.gcode" },
			{ new StringContent("true"), "print" }
		};

		HttpResponseMessage r = await client.SendAsync(request, Ct);

		Assert.Equal(HttpStatusCode.Created, r.StatusCode);
		Assert.Equal("VIRTUAL", session.Port);
		Assert.Equal("slicer-print.gcode", session.CurrentJob?.FileName);
		await session.DisconnectAsync();
	}
}
