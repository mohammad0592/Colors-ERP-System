namespace Colors.Domain.Entities.Shifts;

/// <summary>
/// Somebody who worked a line during a shift. The paper form lists workers and
/// trainees on separate lines, so a trainee is recorded but is not answerable for
/// production.
/// </summary>
public class ShiftWorker
{
    public int Id { get; set; }

    public int ShiftLineId { get; set; }

    public int UserId { get; set; }

    /// <summary>
    /// Which job they did on this shift. A person may hold several roles — the same
    /// man is often both extruder operator and test person — so what he actually did
    /// is a fact about the shift, not about him.
    /// </summary>
    public int? RoleInShiftId { get; set; }

    public bool IsTrainee { get; set; }
}
