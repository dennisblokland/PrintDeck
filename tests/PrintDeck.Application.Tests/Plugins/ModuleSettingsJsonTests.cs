using System.Text.Json.Nodes;

using PrintDeck.Application.Plugins;
using PrintDeck.Application.Tests.TestDoubles;

namespace PrintDeck.Application.Tests.Plugins;

public sealed class ModuleSettingsJsonTests
{
	private static readonly Type _type = typeof(FakeModule.Settings);

	[Fact]
	public void Finds_secret_properties_by_their_json_name()
	{
		Assert.Equal(new Dictionary<string, string> { ["token"] = "Token" }, ModuleSettingsJson.Secrets(_type));
		Assert.Empty(ModuleSettingsJson.Secrets(null));
	}

	[Fact]
	public void Pages_never_get_the_secrets()
	{
		JsonObject saved = new() { ["url"] = "http://x", ["token"] = "SECRET" };

		JsonObject shown = ModuleSettingsJson.WithoutSecrets(saved, _type);

		Assert.Equal("""{"url":"http://x"}""", shown.ToJsonString());
		Assert.Equal("SECRET", (string?) saved["token"]); // the saved settings are untouched
		Assert.Equal(["Token"], ModuleSettingsJson.SavedSecrets(saved, _type));
		Assert.Empty(ModuleSettingsJson.SavedSecrets(new JsonObject { ["token"] = "" }, _type));
	}

	[Theory]
	[InlineData("""{"url":"b","token":""}""", "OLD")]
	[InlineData("""{"url":"b","token":null}""", "OLD")]
	[InlineData("""{"url":"b"}""", "OLD")]
	[InlineData("""{"url":"b","token":"NEW"}""", "NEW")]
	public void An_empty_secret_keeps_the_saved_one(string incoming, string token)
	{
		JsonObject saved = new() { ["url"] = "a", ["token"] = "OLD" };

		JsonObject merged = ModuleSettingsJson.KeepSavedSecrets(JsonNode.Parse(incoming)!.AsObject(), saved, _type);

		Assert.Equal("b", (string?) merged["url"]);
		Assert.Equal(token, (string?) merged["token"]);
	}

	[Fact]
	public void Reads_and_writes_the_module_type()
	{
		JsonObject written = ModuleSettingsJson.Write(new FakeModule.Settings { Url = "u", Token = "t", Minutes = 5 });

		FakeModule.Settings read = ModuleSettingsJson.Read<FakeModule.Settings>(written);

		Assert.Equal(("u", "t", 5), (read.Url, read.Token, read.Minutes));
		Assert.Equal(3, ModuleSettingsJson.Read<FakeModule.Settings>(null).Minutes);
	}
}
