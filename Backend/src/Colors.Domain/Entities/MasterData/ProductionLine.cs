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

    // The three below say what the line *does*. Before they existed every screen
    // offered every open line: a batch could be started on the thermo, which does not
    // mix, and a forming run on the extruder, which has no mould. Nothing in the data
    // said otherwise — only the line's name, and names must never drive logic
    // (specification section 4, questions 14 and 15).
    //
    // Separate flags rather than one line type, because they are not exclusive. A line
    // that both mixes and forms is a factory decision, not a shape to forbid.

    /// <summary>True for the extruder: batches are started here and rolls come off it.</summary>
    public bool MakesRolls { get; set; }

    /// <summary>True for the thermo: rolls go in here and bags come out.</summary>
    public bool FormsBags { get; set; }

    /// <summary>True for the extruder: only this line appears on a material issue ticket.</summary>
    public bool TakesRawMaterial { get; set; }

    /// <summary>
    /// True for the recycler: scrap weighed in and recycled material weighed out are
    /// recorded against this line, and its output goes back into the store
    /// (specification section 11).
    /// </summary>
    public bool Recycles { get; set; }
}
