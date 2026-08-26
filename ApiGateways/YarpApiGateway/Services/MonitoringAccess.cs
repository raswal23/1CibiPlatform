using System.Net;

namespace ApiGateways.YarpApiGateway.Services;

/// <summary>
/// Decides whether a caller may reach the observability endpoints (<c>/metrics</c> and
/// <c>/health</c>) exposed by the gateway.
/// <para>
/// The gateway is the only public entry point for the platform, so these paths would
/// otherwise be reachable from the internet. <c>/metrics</c> enumerates every route
/// name, request count, and error count in the platform, and <c>/health</c> names each
/// backing dependency - both are useful reconnaissance. Access is therefore restricted
/// to the private networks that the monitoring server scrapes from, configured under
/// <c>Monitoring:AllowedScrapeNetworks</c>.
/// </para>
/// <para>
/// Denied requests receive <c>404</c> rather than <c>403</c>: a 403 confirms the
/// endpoint exists, while a 404 is indistinguishable from it never having been mapped.
/// </para>
/// </summary>
public static class MonitoringAccess
{
	/// <summary>
	/// Returns <c>true</c> when <paramref name="remoteIp"/> falls inside one of
	/// <paramref name="allowedCidrs"/>, or when running in Development (where the
	/// endpoints are only bound to localhost anyway and gating them makes local
	/// verification needlessly awkward).
	/// </summary>
	public static bool IsAllowed(
		IPAddress? remoteIp,
		IReadOnlyCollection<string> allowedCidrs,
		IHostEnvironment environment)
	{
		if (environment.IsDevelopment())
		{
			return true;
		}

		if (remoteIp is null)
		{
			return false;
		}

		// Kestrel reports IPv4 callers as IPv4-mapped IPv6 addresses when listening on
		// a dual-stack socket (::ffff:10.0.1.5), which would never match an IPv4 CIDR.
		if (remoteIp.IsIPv4MappedToIPv6)
		{
			remoteIp = remoteIp.MapToIPv4();
		}

		// Loopback covers the container scraping itself and SSH-tunnelled access from
		// an operator's machine, which is the documented way to reach Prometheus.
		if (IPAddress.IsLoopback(remoteIp))
		{
			return true;
		}

		foreach (var cidr in allowedCidrs)
		{
			if (IsInRange(remoteIp, cidr))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Tests an address against a single CIDR block such as <c>10.0.0.0/16</c>. A bare
	/// address without a prefix is treated as an exact match (equivalent to /32 or /128).
	/// Malformed entries return <c>false</c> rather than throwing, so one bad line in
	/// configuration cannot take the gateway down at request time.
	/// </summary>
	private static bool IsInRange(IPAddress address, string cidr)
	{
		if (string.IsNullOrWhiteSpace(cidr))
		{
			return false;
		}

		var parts = cidr.Split('/', 2);

		if (!IPAddress.TryParse(parts[0], out var network))
		{
			return false;
		}

		if (network.AddressFamily != address.AddressFamily)
		{
			return false;
		}

		var addressBytes = address.GetAddressBytes();
		var networkBytes = network.GetAddressBytes();

		var maxPrefix = addressBytes.Length * 8;

		int prefixLength;
		if (parts.Length == 1)
		{
			prefixLength = maxPrefix;
		}
		else if (!int.TryParse(parts[1], out prefixLength)
			|| prefixLength < 0
			|| prefixLength > maxPrefix)
		{
			return false;
		}

		// Compare whole bytes first, then the partial byte at the prefix boundary.
		var wholeBytes = prefixLength / 8;
		var remainingBits = prefixLength % 8;

		for (var i = 0; i < wholeBytes; i++)
		{
			if (addressBytes[i] != networkBytes[i])
			{
				return false;
			}
		}

		if (remainingBits == 0)
		{
			return true;
		}

		var mask = (byte)(0xFF << (8 - remainingBits));

		return (addressBytes[wholeBytes] & mask) == (networkBytes[wholeBytes] & mask);
	}
}
