using Colors.Domain.Common;

namespace Colors.Domain.Entities.MasterData;

/// <summary>
/// The kind of thing a product is — Plate, Meal Box, Clamshell. Used for grouping in
/// reports and to say what a recipe family is for. Never hardcoded (specification
/// section 1).
/// </summary>
public class ProductType : MasterEntity;
