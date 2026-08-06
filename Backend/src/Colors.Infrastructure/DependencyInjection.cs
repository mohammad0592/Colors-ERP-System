using Colors.Application.Features.Authentication;
using Colors.Application.Features.Barcodes;
using Colors.Application.Features.Inventory;
using Colors.Application.Features.MaterialIssue;
using Colors.Application.Features.Production;
using Colors.Application.Features.MasterData;
using Colors.Application.Features.Packaging;
using Colors.Application.Features.Recycler;
using Colors.Application.Features.Pallets;
using Colors.Application.Features.People;
using Colors.Application.Features.Recipes;
using Colors.Application.Features.ShiftReports;
using Colors.Application.Features.Trace;
using Colors.Application.Features.Thermo;
using Colors.Infrastructure.Authentication;
using Colors.Infrastructure.Identity;
using Colors.Infrastructure.Persistence;
using Colors.Infrastructure.Services.Barcodes;
using Colors.Infrastructure.Services.Inventory;
using Colors.Infrastructure.Services.MasterData;
using Colors.Infrastructure.Services.MaterialIssue;
using Colors.Infrastructure.Services.Production;
using Colors.Infrastructure.Services.Packaging;
using Colors.Infrastructure.Services.Recycler;
using Colors.Infrastructure.Services.Pallets;
using Colors.Infrastructure.Services.People;
using Colors.Infrastructure.Services.Recipes;
using Colors.Infrastructure.Services.ShiftReports;
using Colors.Infrastructure.Services.Trace;
using Colors.Infrastructure.Services.Thermo;
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
        services.AddScoped<IMouldService, MouldService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductTypeService, ProductTypeService>();
        services.AddScoped<IMaterialService, MaterialService>();

        // Recipes (specification section 5).
        services.AddScoped<IRecipeService, RecipeService>();

        // Shift reports (specification section 2).
        services.AddScoped<IShiftReportService, ShiftReportService>();

        // People, read only — every screen that has to name somebody.
        services.AddScoped<IPeopleService, PeopleService>();

        // Inventory (specification section 6). The ledger is shared: issue tickets
        // move stock too, and two copies of the locking would drift apart.
        services.AddScoped<StockLedger>();
        services.AddScoped<IInventoryService, InventoryService>();

        // Material issue and return (specification section 7).
        services.AddScoped<IMaterialIssueService, MaterialIssueService>();

        // Barcodes (specification section 12) — needed from the extruder onwards.
        services.AddScoped<IBarcodeService, BarcodeService>();

        // Line 1, the mixer and extruder (specification section 8).
        services.AddScoped<IProductionService, ProductionService>();

        // Line 2, thermoforming (specification section 9).
        services.AddScoped<IThermoService, ThermoService>();

        // Pallets and packaging (specification section 10).
        services.AddScoped<IPalletService, PalletService>();
        services.AddScoped<IPackagingService, PackagingService>();

        // Line 3, the recycler (specification section 11).
        services.AddScoped<IRecyclerService, RecyclerService>();

        // Rolls, bags and pallets as stock, and the labels that go on them
        // (specification sections 8 to 12).
        services.AddScoped<IProducedStockService, ProducedStockService>();

        // Where one thing came from, and what it became (specification section 13).
        services.AddScoped<ITraceService, TraceService>();

        return services;
    }
}
