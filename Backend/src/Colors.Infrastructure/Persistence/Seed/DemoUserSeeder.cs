using Colors.Domain.Constants;
using Colors.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Colors.Infrastructure.Persistence.Seed;

/// <summary>
/// Demonstration accounts, one for each job in the factory, so the system can be
/// tried before any real worker exists.
///
/// These share a single simple password and must never exist on the factory server.
/// Two guards prevent that: the caller only runs this outside production, and it does
/// nothing unless <c>Seed:DemoUsers</c> is explicitly set to true.
///
/// Two of the accounts deliberately hold **two** roles, because that is how the
/// factory works today — the same man runs the extruder and takes its measurements,
/// and the thermo operator also builds the pallets.
/// </summary>
public static class DemoUserSeeder
{
    private const string DefaultPassword = "Colors123";

    private sealed record DemoUser(string EmployeeNumber, string FullName, string[] Roles);

    private static readonly DemoUser[] Users =
    [
        new("SUP001", "Shift Supervisor", [RoleNames.Supervisor]),
        new("INV001", "Inventory Manager", [RoleNames.InventoryManager]),

        // One person, both extruder jobs — the real arrangement in the factory today.
        new("EXT001", "علي حمدان", [RoleNames.ExtruderOperator, RoleNames.ExtruderTestPerson]),

        // The thermo operator also builds the pallets.
        new("THR001", "علي ياغي", [RoleNames.ThermoOperator, RoleNames.PackagingOperator]),

        new("TST001", "صدام نجوم", [RoleNames.ThermoTestPerson]),
        new("REC001", "محمد حمدان", [RoleNames.RecyclerOperator]),
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var configuration = provider.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue<bool>("Seed:DemoUsers"))
        {
            return;
        }

        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DemoUserSeeder));
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var password = configuration["Seed:DemoUserPassword"] ?? DefaultPassword;
        var created = 0;

        foreach (var demo in Users)
        {
            if (await userManager.FindByNameAsync(demo.EmployeeNumber) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = demo.EmployeeNumber,
                EmployeeNumber = demo.EmployeeNumber,
                FullName = demo.FullName,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogError(
                    "Could not create demo user {EmployeeNumber}: {Errors}",
                    demo.EmployeeNumber,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                continue;
            }

            var assigned = await userManager.AddToRolesAsync(user, demo.Roles);
            if (!assigned.Succeeded)
            {
                logger.LogError(
                    "Created demo user {EmployeeNumber} but could not assign roles: {Errors}",
                    demo.EmployeeNumber,
                    string.Join("; ", assigned.Errors.Select(e => e.Description)));
                continue;
            }

            created++;
        }

        if (created > 0)
        {
            logger.LogWarning(
                "Created {Count} DEMO users, all sharing one password. " +
                "These are for trying the system only and must never exist on the factory server.",
                created);
        }
    }
}
