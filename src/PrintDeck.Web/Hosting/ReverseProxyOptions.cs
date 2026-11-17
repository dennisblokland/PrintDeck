namespace PrintDeck.Web.Hosting;

/// <summary>
/// The reverse proxies whose <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> headers PrintDeck trusts, from the
/// <c>ReverseProxy</c> section of appsettings.json (or environment variables such as <c>ReverseProxy__KnownProxies__0</c>).
/// Both lists empty (the default) means the headers are ignored, so a directly exposed PrintDeck can't be fooled by them.
/// </summary>
public sealed class ReverseProxyOptions
{
	public const string Section = "ReverseProxy";

	/// <summary>Proxy addresses, such as <c>127.0.0.1</c> or <c>192.168.1.10</c>.</summary>
	public List<string> KnownProxies { get; set; } = [];

	/// <summary>Proxy networks in CIDR notation, such as <c>172.18.0.0/16</c> for a Docker network.</summary>
	public List<string> KnownNetworks { get; set; } = [];

	public bool IsConfigured => KnownProxies.Count > 0 || KnownNetworks.Count > 0;
}
