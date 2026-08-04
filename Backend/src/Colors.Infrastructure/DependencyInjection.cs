using Colors.Application.Features.Authentication;
using Colors.Application.Features.MasterData;
using Colors.Application.Features.Recipes;
using Colors.Infrastructure.Authentication;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.MasterData;
using Colors.Infrastructure.Services.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Colors.Infrastructure;

/// <summary>
/// Registers everything Infrastructure provides. Program.cs calls one method
/// instead of knowing about DbContext, Npgsql or Identity.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ColorsDb")
            ?? throw new InvalidOperationException(
                "Connection string 'ColorsDb' was not found. " +
                "In development set it with: dotnet user-secrets set \"ConnectionStrings:ColorsDb\" \"...\"");

        services.AddDbContext<ColorsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(ColorsDbContext).Assembly.FullName)));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // Passwords. Workers type these on a tablet with gloves, so the rules
                // stay reachable while still being hashed and never recoverable.
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                // Lockout — five wrong tries locks the account for five minutes.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.AllowedForNewUsers = true;

                // The employee number is the login name, so it must be unique and may
                // contain letters and digits only.
                options.User.RequireUniqueEmail = false;
                options.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ColorsDbContext>();

        // No AddDefaultTokenProviders(): those tokens exist for self-service password
        // reset and email confirmation. Specification section 3 gives password resets to
        // administrators only, and the factory has no email, so an administrator sets a
        // new password directly. It also keeps this layer free of the ASP.NET framework.

        // Token settings are validated when the application starts, not when the first
        // worker tries to log in. A missing or too-short signing key stops the server
        // immediately, with a message saying what is wrong.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // The clock, injected rather than called directly, so tests can move time.
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<JwtTokenGenerator>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        // Master data (specification section 4).
        services.AddScoped<IProductionLineService, ProductionLineService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IUnitService, UnitService>();
        services.AddScoped<IMaterialCategoryService, MaterialCategoryService>();
        services.AddScoped<IColorService, ColorService>();
        services.AddScoped<IPlateSizeService, PlateSizeService>();
        services.AddScoped<IProductTypeService, ProductTypeService>();
        services.AddScoped<IMaterialService, MaterialService>();

        // Recipes (specification section 5).
        services.AddScoped<IRecipeService, RecipeService>();

        return services;
    }
}
