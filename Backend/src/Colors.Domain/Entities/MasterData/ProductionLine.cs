using Colors.Domain.Common;

namespace Colors.Domain.Entities.MasterData;

/// <summary>
/// One of the factory's three lines — Extruder, Thermo, Recycler. One machine each
/// (specification section 1), so the line stands in for the machine.
/// </summary>
public class ProductionLine : MasterEntity;
