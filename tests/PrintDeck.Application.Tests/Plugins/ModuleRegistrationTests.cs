using Microsoft.Extensions.DependencyInjection;

using PrintDeck.Application.Plugins;
using PrintDeck.Application.Tests.TestDoubles;
using PrintDeck.Plugins;

namespace PrintDeck.Application.Tests.Plugins;

public sealed class ModuleRegistrationTests
{
	[Fact]
	public void Registers_the_module_and_lets_it_add_its_services()
	{
		ServiceCollection services = new();
		FakeModule module = new("lights");

		services.AddPrintDeckModule(module);

		Assert.Equal(["configure"], module.Calls);
		Assert.Same(module, Assert.Single(services, d => d.ServiceType == typeof(IPrintDeckModule)).ImplementationInstance);
	}

	[Theory]
	[InlineData("Lights")]
	[InlineData("home assistant")]
	[InlineData("-x")]
	[InlineData("")]
	public void Refuses_ids_that_are_not_lowercase_words(string id)
	{
		Assert.Throws<ArgumentException>(() => new ServiceCollection().AddPrintDeckModule(new FakeModule(id)));
	}

	[Fact]
	public void Refuses_the_same_module_twice()
	{
		ServiceCollection services = new();
		services.AddPrintDeckModule(new FakeModule("lights"));

		Assert.Throws<InvalidOperationException>(() => services.AddPrintDeckModule(new FakeModule("lights")));
	}
}
