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

    /// <summary>
    /// Sets the administrator's password back to whatever <c>Seed:AdminPassword</c>
    /// says.
    ///
    /// This exists because the administrator is the only account nobody else can reset.
    /// Every other password is changed by an administrator (specification section 3),
    /// so if that one is lost there is no way back in except the database.
    ///
    /// Fenced twice, exactly as the demo users are: <c>Program.cs</c> only calls it
    /// outside production, and it does nothing unless <c>Seed:ResetAdminPassword</c> is
    /// explicitly true. The password comes from configuration — user secrets on a
    /// developer's machine — so it is never written into source.
    ///
    /// Turn the flag off again once you are back in.
    /// </summary>
    public static async Task ResetAdministratorPasswordAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var configuration = provider.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue<bool>("Seed:ResetAdminPassword"))
        {
            return;
        }

        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(IdentitySeeder));
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var employeeNumber = configuration["Seed:AdminEmployeeNumber"] ?? "ADMIN001";
        var password = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Seed:ResetAdminPassword is on but Seed:AdminPassword is not set, so nothing changed.");
            return;
        }

        if (await userManager.FindByNameAsync(employeeNumber) is not { } admin)
        {
            logger.LogWarning("No user {EmployeeNumber} to reset.", employeeNumber);
            return;
        }

        // Remove and add rather than a reset token: those tokens are for self-service
        // password reset, which this system deliberately does not have — see the note
        // in DependencyInjection about AddDefaultTokenProviders. An administrator sets
        // a password directly, and this is that same path.
        //
        // Still through Identity, so it is hashed the way every other password is.
        // Writing a hash into the database by hand is how a login silently stops working.
        await userManager.RemovePasswordAsync(admin);
        var result = await userManager.AddPasswordAsync(admin, password);

        if (!result.Succeeded)
        {
            logger.LogError(
                "Could not reset the password for {EmployeeNumber}: {Errors}",
                employeeNumber,
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        logger.LogWarning(
            "The password for {EmployeeNumber} was RESET to the configured one, because " +
            "Seed:ResetAdminPassword is true. Turn that setting off again.",
            employeeNumber);
    }
}
