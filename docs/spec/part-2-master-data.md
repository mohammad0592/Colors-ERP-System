# Styrofoam Factory ERP System — Part 2: Master Data

Version: 1.0

## 1. Introduction

Master data is the relatively static information referenced by every production process. It changes infrequently, unlike production data. Good master data design reduces duplication and eases future expansion.

Covered here: Units, Material Categories, Materials, Product Types, Colors, Plate Sizes, Templates, Recipes.

## 2. Units

Defines how materials are measured.

**Units**
- Id (PK)
- Name
- Symbol

| Id | Name | Symbol |
|---|---|---|
| 1 | Kilogram | kg |
| 2 | Piece | pcs |
| 3 | Roll | roll |
| 4 | Bag | bag |
| 5 | Pallet | pallet |

## 3. Material Categories

Groups materials for filtering and reporting.

**MaterialCategories**
- Id (PK)
- Name
- Description

Categories: Raw Material, Packaging Material, Consumable, Recycled Material.

## 4. Materials

Every physical material used by the factory — production and packaging.

**Materials**
- Id (PK)
- MaterialCode (unique — e.g. `MAT0001`)
- Name
- CategoryId (FK → MaterialCategories)
- UnitId (FK → Units)
- MinimumQuantity
- IsActive
- Notes

> The ERP must never depend on material **name** for identification — always `MaterialCode` / `Id`.

**Raw materials:** GPPS, Recycle, Talc, Nucleating Agent, Absorbent Agent, Antistatic Agent, Coloring Agent.

**Packaging materials:** Tape, Shrink Wrap, Plastic Hood, Large Bags, Small Bags, Empty Wooden Pallets.

## 5. Product Types

Products are stored as data, never hardcoded.

**ProductTypes**
- Id (PK)
- Name
- Description

Examples: Plate, Meal Box, Container.

## 6. Colors

**Colors**
- Id (PK)
- Name

Examples: White, Black, Blue, Green.

## 7. Plate Sizes

The factory currently produces two plate sizes. Every thermo production produces **only one** plate size.

**PlateSizes**
- Id (PK)
- Name

Examples: Large, Small.

## 8. Templates

Templates represent the physical mold used during thermoforming. Currently mainly plate molds; future molds may include meal boxes.

**Templates**
- Id (PK)
- Name
- ProductTypeId (FK → ProductTypes)
- Description

Examples: Large Plate, Small Plate, Meal Box.

## 9. Recipe System

Four main recipe families:

1. Normal (Except Black)
2. Normal Black
3. ABS (Except Black)
4. ABS Black

Each family has multiple versions. The supervisor continuously adjusts ingredient percentages. **Old recipes are never modified** — every change creates a new version.

## 10. Recipe Families

**RecipeFamilies**
- Id (PK)
- Name
- ProductTypeId (FK → ProductTypes)
- UsesRecycleMaterial (bool)
- Description

Examples: Normal, Normal Black, ABS, ABS Black.

## 11. Recipe Versions

Each version is the exact formula used during production. Once used in production a version becomes **immutable** and must never be edited.

**RecipeVersions**
- Id (PK)
- RecipeFamilyId (FK → RecipeFamilies)
- VersionNumber
- Status — `Draft` | `Current` | `Archived`
- CreatedByUserId (FK → Users)
- CreatedDate
- Notes

Normally only one version per family is `Current`.

## 12. Recipe Ingredients

Ingredients live in a separate table so a recipe can have unlimited ingredients.

**RecipeIngredients**
- Id (PK)
- RecipeVersionId (FK → RecipeVersions)
- MaterialId (FK → Materials)
- TargetPercentage
- MinimumPercentage
- MaximumPercentage

## 13. Current Main Recipes

### Family 1 — Normal (Except Black)
| Ingredient | Target |
|---|---|
| GPPS | 100% |
| Talc | 1% |
| Nucleating Agent | 1.5–2% |
| Coloring Agent | 1.5–2% |

### Family 2 — Normal Black
| Ingredient | Target |
|---|---|
| GPPS | 65% |
| Recycle | 35% |
| Talc | 1% |
| Nucleating Agent | 1.5–2% |
| Black Coloring | 2–2.5% |

### Family 3 — ABS (Except Black)
| Ingredient | Target |
|---|---|
| GPPS | 100% |
| Absorbent Agent | 3–4% |
| Coloring | 1.5–2% |
| Antistatic | 1.5–3% |
| Talc | 1% |

### Family 4 — ABS Black
| Ingredient | Target |
|---|---|
| GPPS | 65% |
| Recycle | 35% |
| Absorbent Agent | 3–4% |
| Coloring | 1.5–2% |
| Antistatic | 1.5–3% |
| Talc | 1% |

## 14. Why Versioning Matters

Version 1.0: Recycle = 35%. Six months later, Version 1.1: Recycle = 40%. Both remain in the database forever.

If a customer complains about product manufactured six months ago, management must know the exact formula used. **Every Roll references a RecipeVersion, never a RecipeFamily.**

## 15. Relationships

```
Units             1 ──∞ Materials
MaterialCategories 1 ──∞ Materials
ProductTypes      1 ──∞ Templates
ProductTypes      1 ──∞ RecipeFamilies
RecipeFamilies    1 ──∞ RecipeVersions
RecipeVersions    1 ──∞ RecipeIngredients
Materials         1 ──∞ RecipeIngredients
Users             1 ──∞ RecipeVersions
```

## 16. Design Philosophy

1. Complete historical traceability.
2. No data loss when recipes change.
3. Unlimited future products.
4. Unlimited ingredients per recipe.

---

## Open questions raised during review

Recorded here for resolution; see [open-questions.md](open-questions.md).

1. Percentage basis for `RecipeIngredients` (additives appear to be parts-per-hundred-resin, not % of total).
2. How a generic "Coloring Agent" ingredient resolves to an actual color-specific material at production time.
3. Overlap between `Templates` and `PlateSizes`.
4. `RecipeFamilies.UsesRecycleMaterial` can go stale relative to version ingredients.

---
*End of Part 2.*
