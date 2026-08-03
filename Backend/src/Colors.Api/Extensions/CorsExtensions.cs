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
        IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
            options.AddPolicy(PolicyName, policy =>
            {
                if (origins.Length == 0)
                {
                    // Nothing configured means no browser origin is allowed through.
                    return;
                }

                policy.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }));

        return services;
    }
}
