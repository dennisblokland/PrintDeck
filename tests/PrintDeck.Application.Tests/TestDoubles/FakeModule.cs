using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Plugins;
using PrintDeck.Plugins.Enums;

namespace PrintDeck.Application.Tests.TestDoubles;

/// <summary>A module that records its starts and stops, with a secret in its settings.</summary>
internal sealed class FakeModule(string id = "lights", bool failToStart = false) : IPrintDeckModule
{
	public sealed class Settings
	{
		public string Url { get; set; } = "";

		[Secret]
		public string Token { get; set; } = "";

		public int Minutes { get; set; } = 3;
	}

	public sealed class Card;

	public string Id => id;
	public string Name => $"Module {id}";
	public Version Version { get; } = new(1, 2, 3);
	public Type SettingsType => typeof(Settings);
	public IReadOnlyList<ModuleComponent> Components { get; } = [new(ModuleSlot.Control, typeof(Card))];

	public List<string> Calls { get; } = [];
	public IPluginContext? Context { get; private set; }

	public void ConfigureServices(IServiceCollection services) => Calls.Add("configure");

	public Task StartAsync(IPluginContext context, IServiceProvider services, CancellationToken ct)
	{
		Calls.Add("start");
		if (failToStart) throw new InvalidOperationException("broken");
		Context = context;
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken ct)
	{
		Calls.Add("stop");
		Context = null;
		return Task.CompletedTask;
	}
}
