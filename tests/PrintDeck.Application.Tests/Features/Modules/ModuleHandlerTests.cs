using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging.Abstractions;

using PrintDeck.Application.Abstractions;
using PrintDeck.Application.Abstractions.Enums;
using PrintDeck.Application.Features.Modules.Commands;
using PrintDeck.Application.Features.Modules.Dtos;
using PrintDeck.Application.Features.Modules.Handlers;
using PrintDeck.Application.Features.Modules.Queries;
using PrintDeck.Application.Plugins;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Domain.Common;
using PrintDeck.Domain.Settings;

namespace PrintDeck.Application.Tests.Features.Modules;

public sealed class ModuleHandlerTests
{
	private readonly FakeModule _module = new("lights");
	private readonly ModuleCatalog _catalog;
	private readonly FakeSettingsStore _settings = new();
	private readonly PrinterEventHub _events;
	private readonly List<PrinterChange> _published;

	public ModuleHandlerTests()
	{
		_catalog = new ModuleCatalog([_module]);
		(_events, _published) = RecordingEventHub.Create();
	}

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private ValueTask<Result> Save(string json) =>
		new SaveModuleSettingsHandler(_catalog, _settings, _events).Handle(new SaveModuleSettingsCommand("lights", JsonNode.Parse(json)!.AsObject()), Ct);

	private ValueTask<ModuleSettingsDto?> Get(string id = "lights") =>
		new GetModuleSettingsHandler(_catalog, _settings).Handle(new GetModuleSettingsQuery(id), Ct);

	private ValueTask<Result<JsonObject>> GetSecrets(CurrentUserInfo? user, string id = "lights") =>
		new GetModuleSecretSettingsHandler(_catalog, _settings, new FakeCurrentUser(user)).Handle(new GetModuleSecretSettingsQuery(id), Ct);

	[Fact]
	public async Task Switching_on_saves_and_tells_the_host()
	{
		Result on = await new SetModuleEnabledHandler(_catalog, _settings, _events).Handle(new SetModuleEnabledCommand("lights", true), Ct);

		Assert.True(on.IsSuccess);
		Assert.True(_settings.Settings.Modules["lights"].Enabled);
		Assert.Equal([PrinterChange.Settings], _published);
	}

	[Fact]
	public async Task Unknown_modules_are_refused()
	{
		Assert.Equal(ModuleCatalog.Unknown, (await new SetModuleEnabledHandler(_catalog, _settings, _events).Handle(new SetModuleEnabledCommand("nope", true), Ct)).Error);
		Assert.Equal(ModuleCatalog.Unknown, (await GetSecrets(CurrentUserInfo.ForModule("nope"), "nope")).Error);
		Assert.Null(await Get("nope"));
		Assert.Empty(_settings.Settings.Modules);
	}

	[Fact]
	public async Task Saved_settings_come_back_without_the_secret()
	{
		await Save("""{"url":"http://ha","token":"SECRET","minutes":5}""");

		ModuleSettingsDto dto = (await Get())!;

		Assert.False(dto.Enabled);
		Assert.Equal("""{"url":"http://ha","minutes":5}""", dto.Settings.ToJsonString());
		Assert.Equal(["Token"], dto.SavedSecrets);
		Assert.Equal([PrinterChange.Settings], _published);
	}

	[Fact]
	public async Task Saving_without_the_secret_keeps_it_and_the_switch()
	{
		await new SetModuleEnabledHandler(_catalog, _settings, _events).Handle(new SetModuleEnabledCommand("lights", true), Ct);
		await Save("""{"url":"a","token":"SECRET"}""");

		await Save("""{"url":"b","token":""}""");

		ModuleSection section = _settings.Settings.Modules["lights"];
		Assert.True(section.Enabled);
		Assert.Equal("b", (string?) section.Settings!["url"]);
		Assert.Equal("SECRET", (string?) section.Settings!["token"]);
	}

	[Fact]
	public async Task Settings_that_do_not_fit_the_type_are_refused()
	{
		Result saved = await Save("""{"minutes":"many"}""");

		Assert.Equal(SaveModuleSettingsHandler.DoesNotFit, saved.Error);
		Assert.Empty(_settings.Settings.Modules);
	}

	[Fact]
	public async Task Only_the_module_itself_reads_its_secrets()
	{
		await Save("""{"token":"SECRET"}""");

		Assert.Equal("SECRET", (string?) (await GetSecrets(CurrentUserInfo.ForModule("lights"))).Value!["token"]);
		Assert.Equal(GetModuleSecretSettingsHandler.OwnSecretsOnly, (await GetSecrets(CurrentUserInfo.ForModule("other"))).Error);
		Assert.Equal(GetModuleSecretSettingsHandler.OwnSecretsOnly, (await GetSecrets(FakeCurrentUser.Admin)).Error);
	}

	[Fact]
	public async Task Lists_modules_with_their_switch_and_whether_they_run()
	{
		await using ModuleHost host = new(_catalog, _settings, _events, new FakeModuleContexts(), NullLogger<ModuleHost>.Instance);
		host.Start();
		await new SetModuleEnabledHandler(_catalog, _settings, _events).Handle(new SetModuleEnabledCommand("lights", true), Ct);
		await host.Pending;

		ModuleDto dto = Assert.Single(await new ListModulesHandler(_catalog, host, _settings).Handle(new ListModulesQuery(), Ct));

		Assert.Equal(("lights", "Module lights", "1.2.3", true, true), (dto.Id, dto.Name, dto.Version, dto.Enabled, dto.Running));
		Assert.Equal(_module.Components, dto.Components);
	}

	[Fact]
	public void The_save_command_keeps_its_settings_out_of_logs()
	{
		SaveModuleSettingsCommand command = new("lights", new JsonObject { ["token"] = "SECRET" });

		Assert.DoesNotContain("SECRET", command.ToString());
	}
}
