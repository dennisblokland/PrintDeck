using PrintDeck.Application.Abstractions.Interfaces;
using PrintDeck.Application.Plugins;
using PrintDeck.Plugins;

namespace PrintDeck.Application.Tests.TestDoubles;

/// <summary>Hands out a stand-in context per module and remembers which were released.</summary>
internal sealed class FakeModuleContexts : IModuleContexts
{
	public List<string> Created { get; } = [];
	public List<string> Released { get; } = [];

	public ModuleContextLease Create(string moduleId)
	{
		Created.Add(moduleId);
		return new ModuleContextLease(new StandInContext(moduleId), EmptyServices.Instance, () =>
		{
			Released.Add(moduleId);
			return ValueTask.CompletedTask;
		});
	}

	private sealed class EmptyServices : IServiceProvider
	{
		public static readonly EmptyServices Instance = new();
		public object? GetService(Type serviceType) => null;
	}

	private sealed class StandInContext(string moduleId) : IPluginContext
	{
		public string ModuleId => moduleId;
		public Task<PluginUser?> GetUserAsync() => throw new NotSupportedException();
		public Task<PrinterSnapshot> GetPrinterAsync(CancellationToken ct = default) => throw new NotSupportedException();
		public IDisposable Subscribe(Action<PrinterEvent> handler) => throw new NotSupportedException();
		public Task<PluginResult> SendGcodeAsync(string gcode, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<PluginResult> StartPrintAsync(string fileName, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<PluginResult> PauseAsync(CancellationToken ct = default) => throw new NotSupportedException();
		public Task<PluginResult> ResumeAsync(CancellationToken ct = default) => throw new NotSupportedException();
		public Task<PluginResult> CancelAsync(CancellationToken ct = default) => throw new NotSupportedException();
		public Task<PluginResult> LogAsync(string message, CancellationToken ct = default) => throw new NotSupportedException();
		public Task<bool> IsEnabledAsync(CancellationToken ct = default) => throw new NotSupportedException();
		public Task<PluginResult<T>> GetSettingsAsync<T>(CancellationToken ct = default) where T : class, new() => throw new NotSupportedException();
		public Task<EditableSettings<T>> GetEditableSettingsAsync<T>(CancellationToken ct = default) where T : class, new() => throw new NotSupportedException();
		public Task<PluginResult> SaveSettingsAsync<T>(T settings, CancellationToken ct = default) where T : class, new() => throw new NotSupportedException();
	}
}
