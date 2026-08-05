using Colors.Domain.Common;

namespace Colors.Domain.Entities.MasterData;

/// <summary>
/// A forming template bolted into the thermo machine — the factory says "template",
/// the trade says mould (specification section 4).
///
/// Changing one is heavy work, so a mould is mounted at the start of a shift and runs
/// all shift. That is why it belongs to the shift's forming line rather than to each
/// roll.
/// </summary>
public class Mould : MasterEntity;
