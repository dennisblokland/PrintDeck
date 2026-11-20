using Mediator;

using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Behaviors;
using PrintDeck.Application.Features.HostControl.Commands;
using PrintDeck.Application.Features.Modules.Commands;
using PrintDeck.Application.Features.Modules.Dtos;
using PrintDeck.Application.Features.Modules.Queries;
using PrintDeck.Application.Plugins;
using PrintDeck.Application.Printer.Interfaces;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.HostControl.Enums;
using PrintDeck.Domain.Users.Enums;
using PrintDeck.Modules.HomeAssistant;
using PrintDeck.Modules.HomeAssistant.Client;
using PrintDeck.Modules.HomeAssistant.Light;
using PrintDeck.Modules.HomeAssistant.Light.Enums;
using PrintDeck.Plugins;

namespace PrintDeck.Web.Tests.Plugins;

/// <summary>The Home Assistant module in the real app: switched on, configured and used through PrintDeck's own commands.</summary>
public sealed class HomeAssistantModuleTests : IAsyncLifetime
{
	private static readonly CurrentUserInfo _admin = new("ada", UserRole.Admin);
	private static readonly CurrentUserInfo _viewer = new("vic", UserRole.Viewer);

	private readonly PrintDeckWebFactory _app = new();
	private readonly List<AsyncServiceScope> _scopes = [];

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	public ValueTask InitializeAsync()
	{
		_ = _app.Services; // starts the app
		return ValueTask.CompletedTask;
	}

	public async ValueTask DisposeAsync()
	{
		foreach (AsyncServiceScope scope in _scopes) await scope.DisposeAsync();
		await _app.DisposeAsync();
	}

	/// <summary>A scope acting as <paramref name="user"/>, as a Blazor circuit would.</summary>
	private IServiceProvider As(CurrentUserInfo user)
	{
		AsyncServiceScope scope = _app.Services.CreateAsyncScope();
		_scopes.Add(scope);
		scope.ServiceProvider.GetRequiredService<ScopeIdentity>().User = user;
		return scope.ServiceProvider;
	}

	private PluginContext Context(CurrentUserInfo user) =>
		As(user).GetRequiredService<PluginContextFactory>().Create(HomeAssistantModule.ModuleId);

	private ModuleHost Host => _app.Services.GetRequiredService<ModuleHost>();

	private async Task SwitchOn()
	{
		Result on = await As(_admin).GetRequiredService<IMediator>().Send(new SetModuleEnabledCommand(HomeAssistantModule.ModuleId, true), Ct);
		Assert.True(on.IsSuccess);
		await Eventually(() => Host.IsRunning(HomeAssistantModule.ModuleId));
	}

	private static async Task Eventually(Func<bool> condition)
	{
		DateTime deadline = DateTime.UtcNow.AddSeconds(10);
		while (!condition())
		{
			if (DateTime.UtcNow > deadline) throw new TimeoutException();
			await Task.Delay(20, Ct);
		}
	}

	[Fact]
	public async Task The_module_is_listed_and_off_until_switched_on()
	{
		IReadOnlyList<ModuleDto> modules = await As(_viewer).GetRequiredService<IMediator>().Send(new ListModulesQuery(), Ct);

		ModuleDto module = Assert.Single(modules);
		Assert.Equal(("Home Assistant", false, false), (module.Name, module.Enabled, module.Running));

		await SwitchOn();
		Assert.True(Assert.Single(await As(_viewer).GetRequiredService<IMediator>().Send(new ListModulesQuery(), Ct)).Running);
	}

	[Fact]
	public async Task Only_admins_switch_modules()
	{
		Result refused = await As(_viewer).GetRequiredService<IMediator>().Send(new SetModuleEnabledCommand(HomeAssistantModule.ModuleId, true), Ct);

		Assert.Equal(AuthorizationBehavior<SetModuleEnabledCommand, Result>.AdminRequired, refused.Error);
	}

	[Fact]
	public async Task The_token_is_saved_but_never_shown_again()
	{
		await SwitchOn();
		PluginResult saved = await Context(_admin).SaveSettingsAsync(new HomeAssistantSettings { Url = "VIRTUAL", Token = "SECRET", LightEntity = VirtualHomeAssistant.Light });
		Assert.True(saved.IsSuccess);

		EditableSettings<HomeAssistantSettings> shown = await Context(_viewer).GetEditableSettingsAsync<HomeAssistantSettings>(Ct);
		Assert.Equal(("VIRTUAL", "", VirtualHomeAssistant.Light), (shown.Settings.Url, shown.Settings.Token, shown.Settings.LightEntity));
		Assert.True(shown.IsSaved(nameof(HomeAssistantSettings.Token)));

		// Only the module's own context reads the token; an admin's page context doesn't.
		Assert.False((await Context(_admin).GetSettingsAsync<HomeAssistantSettings>(Ct)).IsSuccess);

		Assert.False((await Context(_viewer).SaveSettingsAsync(new HomeAssistantSettings(), Ct)).IsSuccess);
	}

	[Fact]
	public async Task An_admin_switches_the_virtual_light_and_the_terminal_says_so()
	{
		await SwitchOn();
		await Context(_admin).SaveSettingsAsync(new HomeAssistantSettings { Url = "VIRTUAL", LightEntity = VirtualHomeAssistant.Light }, Ct);
		PrinterLight light = _app.Services.GetRequiredService<PrinterLight>();

		PluginResult on = await light.SetAsync(true, Context(_admin), LightSource.Manual, Ct);

		Assert.True(on.IsSuccess);
		Assert.True((await light.GetStateAsync(Ct)).Value);
		Assert.Contains(_app.Services.GetRequiredService<IPrinterSession>().Terminal(10), e => e.Text == "Light on through Home Assistant");
		Assert.Equal(PrinterLight.AdminsOnly, (await light.SetAsync(false, Context(_viewer), LightSource.Manual, Ct)).Error);
	}

	[Fact]
	public async Task Switched_off_it_stops_and_its_light_with_it()
	{
		await SwitchOn();
		await Context(_admin).SaveSettingsAsync(new HomeAssistantSettings { Url = "VIRTUAL", LightEntity = VirtualHomeAssistant.Light }, Ct);

		await As(_admin).GetRequiredService<IMediator>().Send(new SetModuleEnabledCommand(HomeAssistantModule.ModuleId, false), Ct);
		await Eventually(() => !Host.IsRunning(HomeAssistantModule.ModuleId));

		Assert.Equal(HomeAssistantErrors.Off, (await _app.Services.GetRequiredService<PrinterLight>().GetStateAsync(Ct)).Error);
	}

	[Fact]
	public async Task A_module_acting_on_its_own_may_not_run_host_commands()
	{
		Result refused = await As(CurrentUserInfo.ForModule(HomeAssistantModule.ModuleId)).GetRequiredService<IMediator>()
			.Send(new RunHostActionCommand(HostAction.RestartPrintDeck), Ct);

		Assert.Equal(AuthorizationBehavior<RunHostActionCommand, Result>.HostAccountRequired, refused.Error);
	}
}
