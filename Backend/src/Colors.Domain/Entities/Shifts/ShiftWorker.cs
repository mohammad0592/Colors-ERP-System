namespace Colors.Domain.Entities.Shifts;

/// <summary>
/// Somebody who worked a line during a shift — the line's staff. The paper form lists
/// workers and trainees on separate lines, so a trainee is recorded but is not
/// answerable for production.
///
/// Either a system user or a staff member with no account (specification section 19.19),
/// never both and never neither: the database holds that rule too.
/// </summary>
public class ShiftWorker
{
    public int Id { get; set; }

    public int ShiftLineId { get; set; }

    /// <summary>A system user. Null when this is a staff member with no account.</summary>
    public int? UserId { get; set; }

    /// <summary>Somebody with no account, only recorded. Null for a system user.</summary>
    public int? StaffMemberId { get; set; }

    public StaffMember? StaffMember { get; set; }

    /// <summary>
    /// One fact about this man on this shift, which is why it lives here and not on
    /// each job. Repeated per job it could disagree with itself — a trainee as an
    /// operator, not a trainee as a test person, same man, same night.
    /// </summary>
    public bool IsTrainee { get; set; }

    /// <summary>
    /// The jobs he did, which is a list because that is how the factory runs: the same
    /// man usually runs the extruder and takes its measurements, and the thermo
    /// operator also builds the pallets.
    ///
    /// Still not the same as the roles he holds. A man may hold four and work two of
    /// them tonight; only the shift can say which.
    /// </summary>
    public List<ShiftWorkerRole> Roles { get; set; } = [];
}
