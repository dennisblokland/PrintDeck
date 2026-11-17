using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

using PrintDeck.Web.Hosting;

namespace PrintDeck.Web.Tests.Hosting;

public class ReverseProxySetupTests
{
	[Fact]
	public void Nothing_configured_trusts_nobody_not_even_loopback()
	{
		ForwardedHeadersOptions options = new();
		ReverseProxySetup.Apply(options, new ReverseProxyOptions());

		Assert.Equal(ForwardedHeaders.None, options.ForwardedHeaders);
		Assert.Empty(options.KnownProxies);
		Assert.Empty(options.KnownIPNetworks);
	}

	[Fact]
	public void Configured_proxies_replace_the_loopback_default()
	{
		ForwardedHeadersOptions options = new();
		ReverseProxySetup.Apply(options, new ReverseProxyOptions { KnownProxies = ["192.168.1.10", " ::1 "], KnownNetworks = ["172.18.0.0/16"] });

		Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
		Assert.Equal(1, options.ForwardLimit);
		Assert.Equal([IPAddress.Parse("192.168.1.10"), IPAddress.IPv6Loopback], options.KnownProxies);
		Assert.Equal([System.Net.IPNetwork.Parse("172.18.0.0/16")], options.KnownIPNetworks);
	}

	[Theory]
	[InlineData("printer.local", null, "ReverseProxy:KnownProxies: 'printer.local' is not an IP address.")]
	[InlineData(null, "172.18.0.0", "ReverseProxy:KnownNetworks: '172.18.0.0' is not a network like 192.168.1.0/24.")]
	public void A_typo_stops_the_start_instead_of_silently_trusting_nobody(string? proxy, string? network, string message)
	{
		ReverseProxyOptions configured = new()
		{
			KnownProxies = proxy is null ? [] : [proxy],
			KnownNetworks = network is null ? [] : [network]
		};

		InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ReverseProxySetup.Apply(new ForwardedHeadersOptions(), configured));
		Assert.Equal(message, error.Message);
	}
}
