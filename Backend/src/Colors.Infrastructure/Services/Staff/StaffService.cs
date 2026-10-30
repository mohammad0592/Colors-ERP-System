using Colors.Application.Common.Models;
using Colors.Application.Features.Staff;
using Colors.Domain.Entities.Shifts;
using Colors.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Colors.Infrastructure.Services.Staff;

/// <summary>
/// Staff who work the floor without using the system (specification section 19.19).
/// </summary>
public class StaffService(ColorsDbContext db, TimeProvider timeProvider) : IStaffService
{
    private const int MaxNameLength = 100;
    private const int MaxNumberLength = 20;

    public async Task<IReadOnlyList<StaffMemberDto>> GetAllAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        return await db.StaffMembers
            .Where(s => includeInactive || s.IsActive)
            .OrderBy(s => s.FullName)
            .Select(s => new StaffMemberDto(s.Id, s.FullName, s.EmployeeNumber, s.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<StaffMemberDto>> CreateAsync(
        SaveStaffMemberRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var (name, number) = (Trimmed(request.FullName), Trimmed(request.EmployeeNumber));
        var error = await ErrorAsync(name, number, exceptId: null, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var member = new StaffMember
        {
            FullName = name!,
            EmployeeNumber = number,
            IsActive = request.IsActive,
            CreatedByUserId = userId,
            CreatedAt = timeProvider.GetUtcNow(),
        };
        db.StaffMembers.Add(member);
        await db.SaveChangesAsync(cancellationToken);

        return Result<StaffMemberDto>.Success(ToDto(member));
    }

    public async Task<Result<StaffMemberDto>> UpdateAsync(
        int id,
        SaveStaffMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var member = await db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (member is null)
        {
            return Result<StaffMemberDto>.Failure(
                ErrorCode.NotFound, "This worker does not exist.", "staff.notFound");
        }

        var (name, number) = (Trimmed(request.FullName), Trimmed(request.EmployeeNumber));
        var error = await ErrorAsync(name, number, exceptId: id, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        // Renamed or switched off, never deleted: the shifts he worked go on naming him.
        member.FullName = name!;
        member.EmployeeNumber = number;
        member.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);

        return Result<StaffMemberDto>.Success(ToDto(member));
    }

    private async Task<Result<StaffMemberDto>?> ErrorAsync(
        string? name,
        string? number,
        int? exceptId,
        CancellationToken cancellationToken)
    {
        if (name is null)
        {
            return Invalid("Type the worker's name.", "staff.nameRequired");
        }

        if (name.Length > MaxNameLength || number?.Length > MaxNumberLength)
        {
            return Invalid(
                $"A name is at most {MaxNameLength} characters and a number at most {MaxNumberLength}.",
                "staff.tooLong");
        }

        if (number is null)
        {
            return null;
        }

        // The number names one person. A system user who has it is already on the list
        // to choose from, and a second record for him would split his shifts in two.
        if (await db.Users.AnyAsync(u => u.EmployeeNumber == number, cancellationToken))
        {
            return Invalid(
                $"{number} belongs to a staff member with an account. Choose him from the list instead.",
                "staff.numberIsUser",
                number);
        }

        return await db.StaffMembers.AnyAsync(
                s => s.EmployeeNumber == number && (exceptId == null || s.Id != exceptId),
                cancellationToken)
            ? Invalid($"Another worker already has the number {number}.", "staff.numberTaken", number)
            : null;
    }

    private static StaffMemberDto ToDto(StaffMember s) =>
        new(s.Id, s.FullName, s.EmployeeNumber, s.IsActive);

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<StaffMemberDto> Invalid(string message, string code, params string[] args) =>
        Result<StaffMemberDto>.Failure(ErrorCode.ValidationFailed, message, code, args);
}
