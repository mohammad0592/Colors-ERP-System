namespace Colors.Application.Features.People;

/// <summary>
/// A person, as any screen that has to name one needs them. Specification section 3.
///
/// This is a lookup, not user management: no email, no lockout, nothing that could be
/// changed from here. Full user administration is its own feature.
/// </summary>
public sealed record PersonDto(
    int Id,
    string EmployeeNumber,
    string FullName,
    bool IsActive,
    IReadOnlyList<string> Roles);

/// <summary>A role, for the "what was this worker doing on the shift?" choice.</summary>
public sealed record RoleDto(int Id, string Name);

/// <summary>
/// Reading people and roles. Declared here, implemented in Infrastructure — users live
/// in ASP.NET Identity, which the rest of the application must not know about.
/// </summary>
public interface IPeopleService
{
    Task<IReadOnlyList<PersonDto>> GetPeopleAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default);
}
