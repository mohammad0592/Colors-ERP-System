using Colors.Domain.Constants;
using Colors.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Colors.Infrastructure.Persistence.Seed;

/// <summary>
/// Creates the nine roles, and the first administrator account.
///
/// Runs on every startup and is safe to run again: it only adds what is missing.
/// Specification section 3 lists the roles; section 12 of the build order notes that
/// a fresh database needs a first administrator, because there is no public
/// registration page and nobody could otherwise log in.
/// </summary>
public static class IdentitySeeder
{
    private static readonly Dictionary<string, string> RoleDescriptions = new()
    {
        [RoleNames.Administrator] = "Users, master data, recipes, system settings, backups, corrections.",
        [RoleNames.Supervisor] = "Recipe versions, inventory adjustments, opening and closing shifts, approving corrections.",
        [RoleNames.InventoryManager] = "Receives materials, issues tickets, records returns.",
        [RoleNames.ExtruderOperator] = "Line 1 — creates batches and rolls, prints roll barcodes.",
        [RoleNames.ExtruderTestPerson] = "Line 1 — records roll test reports: weight, length, plate weight, thickness.",
        [RoleNames.ThermoOperator] = "Line 2 — consumes rolls, records forming, creates produced bags.",
        [RoleNames.ThermoTestPerson] = "Line 2 — records thermo test reports: plate weight, absorbent percentage.",
        [RoleNames.PackagingOperator] = "Line 2 — creates pallets, scans bags onto them, records packaging materials.",
        [RoleNames.RecyclerOperator] = "Line 3 — records scrap weight and recycled output.",
    };

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(IdentitySeeder));
        var roleManager = provider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        await SeedRolesAsync(roleManager, logger);
        await SeedFirstAdministratorAsync(userManager, configuration, logger);
    }

    private static async Task SeedRolesAsync(
        RoleManager<ApplicationRole> roleManager,
        ILogger logger)
    {
        foreach (var roleName in RoleNames.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new ApplicationRole
            {
                Name = roleName,
                Description = RoleDescriptions[roleName],
            });

            if (result.Succeeded)
            {
                logger.LogInformation("Created role {RoleName}.", roleName);
            }
            else
            {
                logger.LogError(
                    "Could not create role {RoleName}: {Errors}",
                    roleName,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task SeedFirstAdministratorAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        var employeeNumber = configuration["Seed:AdminEmployeeNumber"] ?? "ADMIN001";
        var fullName = configuration["Seed:AdminFullName"] ?? "System Administrator";
        var password = configuration["Seed:AdminPassword"];

        if (await userManager.FindByNameAsync(employeeNumber) is not null)
        {
            return;
        }

        // No password is ever hardcoded. Without one configured we create nothing and
        // say how — a default password shipped in source is how systems get broken into.
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No administrator account exists and Seed:AdminPassword is not set, so none was created. " +
                "Set it with: dotnet user-secrets set \"Seed:AdminPassword\" \"<password>\" --project src/Colors.Api");
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = employeeNumber,
            EmployeeNumber = employeeNumber,
            FullName = fullName,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var created = await userManager.CreateAsync(admin, password);
        if (!created.Succeeded)
        {
            logger.LogError(
                "Could not create the first administrator: {Errors}",
                string.Join("; ", created.Errors.Select(e => e.Description)));
            return;
        }

        var assigned = await userManager.AddToRoleAsync(admin, RoleNames.Administrator);
        if (!assigned.Succeeded)
        {
            logger.LogError(
                "Created administrator {EmployeeNumber} but could not assign the role: {Errors}",
                employeeNumber,
                string.Join("; ", assigned.Errors.Select(e => e.Description)));
            return;
        }

        logger.LogInformation(
            "Created the first administrator {EmployeeNumber}. Change this password after the first login.",
            employeeNumber);
    }
}
