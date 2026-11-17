using System.Net;

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace PrintDeck.Web.Hosting;

public static class ReverseProxySetup
{
	/// <summary>
	/// Binds <see cref="ReverseProxyOptions"/> and turns them into <see cref="ForwardedHeadersOptions"/>. Only the
	/// configured proxies are trusted (not even loopback by default), and only the last hop is taken, which is the
	/// address the proxy itself saw.
	/// </summary>
	public static IServiceCollection AddPrintDeckReverseProxy(this IServiceCollection services)
	{
		services.AddOptions<ReverseProxyOptions>().BindConfiguration(ReverseProxyOptions.Section);
		services.AddOptions<ForwardedHeadersOptions>()
			.Configure<IOptions<ReverseProxyOptions>>((o, proxy) => Apply(o, proxy.Value));
		return services;
	}

	/// <summary>
	/// Rewrites the client address and scheme from trusted proxies' headers. Goes first in the pipeline, so the
	/// login rate limit, authentication and redirects see the real client. Does nothing until proxies are configured.
	/// </summary>
	public static IApplicationBuilder UsePrintDeckForwardedHeaders(this IApplicationBuilder app) =>
		app.ApplicationServices.GetRequiredService<IOptions<ReverseProxyOptions>>().Value.IsConfigured
			? app.UseForwardedHeaders()
			: app;

	public static void Apply(ForwardedHeadersOptions options, ReverseProxyOptions proxy)
	{
		options.KnownProxies.Clear();
		options.KnownIPNetworks.Clear();
		if (!proxy.IsConfigured)
		{
			options.ForwardedHeaders = ForwardedHeaders.None;
			return;
		}

		options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
		options.ForwardLimit = 1;
		foreach (string address in proxy.KnownProxies)
			options.KnownProxies.Add(IPAddress.TryParse(address.Trim(), out IPAddress? ip)
				? ip
				: throw new InvalidOperationException($"{ReverseProxyOptions.Section}:KnownProxies: '{address}' is not an IP address."));
		foreach (string network in proxy.KnownNetworks)
			options.KnownIPNetworks.Add(System.Net.IPNetwork.TryParse(network.Trim(), out System.Net.IPNetwork net)
				? net
				: throw new InvalidOperationException($"{ReverseProxyOptions.Section}:KnownNetworks: '{network}' is not a network like 192.168.1.0/24."));
	}
}
