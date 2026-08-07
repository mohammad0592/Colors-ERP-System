using System.Net;

namespace Colors.Api.Extensions;

/// <summary>
/// Lets the React application talk to the API.
///
/// In production both are served from the same Windows Server, so no cross-origin
/// request happens at all. This exists for development, where Vite runs on its own
/// port. The allowed origins come from configuration and are never a wildcard —
/// a wildcard would let any page on the factory network call the API.
/// </summary>
public static class CorsExtensions
{
    public const string PolicyName = "ColorsFrontend";

    public static IServiceCollection AddFrontendCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        // Whether a phone or tablet on the same network may be used while developing.
        // Off unless asked for, and ignored entirely outside Development.
        var allowLocalNetwork =
            environment.IsDevelopment()
            && configuration.GetValue("Cors:AllowLocalNetworkInDevelopment", false);

        services.AddCors(options =>
            options.AddPolicy(PolicyName, policy =>
            {
                if (origins.Length == 0 && !allowLocalNetwork)
                {
                    // Nothing configured means no browser origin is allowed through.
                    return;
                }

                if (origins.Length > 0)
                {
                    policy.WithOrigins(origins);
                }

                if (allowLocalNetwork)
                {
                    // Still not a wildcard. Only addresses on this network are allowed,
                    // and only while developing — the machine's own address changes with
                    // DHCP, so listing it would break on a reboot and look like a bug in
                    // the code.
                    policy.SetIsOriginAllowed(IsOnThisNetwork);
                }

                policy.AllowAnyHeader().AllowAnyMethod();
            }));

        return services;
    }

    /// <summary>
    /// True for an origin whose host is a private address — the ranges a home or factory
    /// network hands out. Anything on the public internet is refused.
    /// </summary>
    private static bool IsOnThisNetwork(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.IsLoopback)
        {
            return true;
        }

        if (!IPAddress.TryParse(uri.Host, out var address)
            || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // A name rather than an address. Resolving it would let anybody who controls
            // a DNS entry choose their own answer, so it is refused.
            return false;
        }

        var octets = address.GetAddressBytes();

        return octets[0] switch
        {
            10 => true,
            172 => octets[1] >= 16 && octets[1] <= 31,
            192 => octets[1] == 168,
            _ => false,
        };
    }
}
