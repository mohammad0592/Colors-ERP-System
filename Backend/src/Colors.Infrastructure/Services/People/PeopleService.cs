using Colors.Application.Features.People;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.People;

/// <summary>
/// Reads people and roles out of ASP.NET Identity for the screens that must name
/// somebody — who supervised a shift, who worked it.
/// </summary>
public class PeopleService(ColorsDbContext db) : IPeopleService
{
    public async Task<IReadOnlyList<PersonDto>> GetPeopleAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var users = await db.Users
            .Where(u => includeInactive || u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.EmployeeNumber, u.FullName, u.IsActive })
            .ToListAsync(cancellationToken);

        // Roles come from the join table in one query rather than one per person.
        var roles = await db.UserRoles
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync(cancellationToken);

        var byUser = roles
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                IReadOnlyList<string> (g) => g.Select(x => x.Name ?? string.Empty).Order().ToList());

        return users
            .Select(u => new PersonDto(
                u.Id,
                u.EmployeeNumber,
                u.FullName,
                u.IsActive,
                byUser.GetValueOrDefault(u.Id, [])))
            .ToList();
    }

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        return await db.Roles
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name ?? string.Empty))
            .ToListAsync(cancellationToken);
    }
}
