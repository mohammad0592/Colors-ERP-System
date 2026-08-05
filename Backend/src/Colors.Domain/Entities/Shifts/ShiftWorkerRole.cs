namespace Colors.Domain.Entities.Shifts;

/// <summary>
/// One job a worker did during a shift (specification section 2).
///
/// A row rather than a column on <see cref="ShiftWorker"/>, because one man commonly
/// does two: forcing a single choice would make the record say he ran the extruder and
/// say nothing about the testing he also did.
/// </summary>
public class ShiftWorkerRole
{
    public int Id { get; set; }

    public int ShiftWorkerId { get; set; }

    /// <summary>One of the nine roles from section 3.</summary>
    public int RoleId { get; set; }
}
