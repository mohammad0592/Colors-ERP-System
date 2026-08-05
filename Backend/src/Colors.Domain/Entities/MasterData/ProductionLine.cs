using Colors.Domain.Common;

namespace Colors.Domain.Entities.MasterData;

/// <summary>
/// One of the factory's three lines — Extruder, Thermo, Recycler. One machine each
/// (specification section 1), so the line stands in for the machine.
/// </summary>
public class ProductionLine : MasterEntity
{
    /// <summary>
    /// True only for the thermo line, which is the one whose shift report records
    /// forming speed, feed distance and cycle time. The extruder and the recycler have
    /// no such settings, so their shift report never asks for them.
    ///
    /// A flag on the line rather than a check on its name: the factory may rename a
    /// line or add a second thermo, and neither should need a code change.
    /// </summary>
    public bool RecordsMachineSettings { get; set; }
}
