namespace ProcSight.Infrastructure.Providers;
public static class LocalEndpointPolicy
{
    public static void EnsureLocal(Uri endpoint)
    {
        if(!endpoint.IsAbsoluteUri || endpoint.Scheme is not ("http" or "https") ||
            !(endpoint.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase) ||
              System.Net.IPAddress.TryParse(endpoint.Host,out var address)&&System.Net.IPAddress.IsLoopback(address)))
            throw new ArgumentException("Local model endpoint must be loopback HTTP or HTTPS.");
    }
}
