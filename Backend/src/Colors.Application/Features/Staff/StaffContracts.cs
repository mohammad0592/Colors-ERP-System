using Colors.Application.Common.Models;

namespace Colors.Application.Features.Staff;

// Staff who work the floor without using the system: no account, no password, no role
// (specification section 19.19). Only recorded on the shifts they work.

public sealed record StaffMemberDto(
    int Id,
    string FullName,
    string? EmployeeNumber,
    bool IsActive);

/// <summary>Adds or corrects a staff member. The number is optional.</summary>
public sealed record SaveStaffMemberRequest(
    string FullName,
    string? EmployeeNumber,
    bool IsActive = true);

/// <summary>Staff without an account. Declared here, implemented in Infrastructure.</summary>
public interface IStaffService
{
    Task<IReadOnlyList<StaffMemberDto>> GetAllAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<Result<StaffMemberDto>> CreateAsync(
        SaveStaffMemberRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<Result<StaffMemberDto>> UpdateAsync(
        int id,
        SaveStaffMemberRequest request,
        CancellationToken cancellationToken = default);
}
