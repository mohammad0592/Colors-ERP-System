using System.Text;
using Colors.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Colors.Api.Extensions;

/// <summary>
/// Teaches the API how to read and check the access token on every request.
/// The token is created in Infrastructure; this only validates what arrives.
/// </summary>
public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                "The 'Jwt' configuration section is missing. In development set the signing key with: " +
                "dotnet user-secrets set \"Jwt:SigningKey\" \"<at least 32 characters>\" --project src/Colors.Api");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Every one of these is checked. Turning any of them off would let a
                    // token from somewhere else be accepted here.
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),

                    ValidateLifetime = true,

                    // Default is five minutes, which would keep an expired token alive
                    // that much longer. Zero means expiry happens when it says it does.
                    ClockSkew = TimeSpan.Zero,
                };

                // Tokens travel over the network, so HTTPS is required except while
                // developing on this machine (specification section 15).
                options.RequireHttpsMetadata = !configuration.GetValue<bool>("Jwt:AllowHttpInDevelopment");
            });

        services.AddAuthorization();

        return services;
    }
}
