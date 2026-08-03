# Styrofoam Factory ERP System — Part 9: Database Design and ERD

Version: 1.0

## 1. Introduction

The database is designed around one primary objective: **complete production traceability**. Every physical item can be traced from raw materials to the final finished pallet.

Relational principles, implemented in PostgreSQL. Integer identity primary keys. Foreign keys enforce relationships. Historical production records are never deleted — they remain permanently for reporting and traceability.

## 2. Design Principles

**1. Avoid data duplication** — information stored once wherever possible. Roll weight lives only in `RollTestReports`, never repeated in `ThermoTestReports`.

**2. Historical data is permanent** — production history is never deleted, recipe versions are never edited, inventory history is never removed.

**3. Separation of master and transaction data**
- Master: Materials, Colors, ProductTypes, PlateSizes, Recipes
- Transaction: Rolls, ThermoProductions, ProducedBags, InventoryMovements, ShiftReports

**4. Traceability first** — every production table references the previous stage:

```
Produced Bag → Thermo Production → Roll → Recipe Version → Recipe Ingredients → Materials
```

## 3. Master Tables

Units · MaterialCategories · Materials · Colors · ProductTypes · PlateSizes · Templates · RecipeFamilies · RecipeVersions · RecipeIngredients · Users · Roles

## 4. Production Tables

ShiftReports · ShiftWorkers · Rolls · RollTestReports · ThermoProductions · ThermoTestReports · ProducedBags · WoodenPallets · BagPalletAssignments · RecyclerProductions · PackagingMaterialConsumption

## 5. Inventory Tables

- **Inventory** — current stock only
- **InventoryMovements** — every stock change, permanently

## 6. Main Relationships

| Parent | | Child |
|---|---|---|
| RecipeFamily | 1 → ∞ | RecipeVersions |
| RecipeVersion | 1 → ∞ | RecipeIngredients |
| Material | 1 → ∞ | RecipeIngredients |
| RecipeVersion | 1 → ∞ | Rolls |
| ShiftReport | 1 → ∞ | Rolls |
| Roll | 1 → 1 | RollTestReport |
| Roll | 1 → 1 | ThermoProduction |
| ThermoProduction | 1 → 1 | ThermoTestReport |
| ThermoProduction | 1 → ∞ | ProducedBags |
| WoodenPallet | 1 → ∞ | BagPalletAssignments |
| ProducedBag | 1 → 1 | BagPalletAssignment |
| ShiftReport | 1 → 1 | RecyclerProduction |
| ShiftReport | 1 → 1 | PackagingMaterialConsumption |
| Inventory | 1 → ∞ | InventoryMovements |

## 7. Complete Production Chain

```
Recipe Family → Recipe Version → Roll → Roll Test Report → Thermo Production
  → Thermo Test Report → Produced Bags → Bag Assignment → Wooden Pallet → Inventory
```

## 8. Primary Keys

Every table uses a single integer identity key — fast indexing, small storage, easy foreign keys.

## 9. Foreign Keys

Foreign keys enforce integrity: `Rolls.RecipeVersionId` must reference `RecipeVersions.Id`. This prevents invalid production records.

## 10. Cascade Delete Strategy

Production history is never deleted automatically. **Restrict Delete** on master data, production data and inventory history alike.

If a master record is no longer used, mark it inactive rather than deleting it.

## 11. Index Recommendations

MaterialCode · RollCode · Barcode · RecipeVersionId · ShiftReportId · ProducedDate · Status · Inventory Reference

## 12. Barcode Fields

`Rolls.Barcode`, `ProducedBags.Barcode`, `WoodenPallets.Barcode` — every barcode unique, enforced by a unique database constraint.

## 13. Audit Fields

Most transactional tables should contain `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`.

## 14. Soft Delete

Master tables support soft deletion via `IsActive` — Materials, RecipeFamilies, Templates, Colors. Historical production records remain valid.

## 15. Table Summary

**Master:** Units, MaterialCategories, Materials, Colors, ProductTypes, PlateSizes, Templates, RecipeFamilies, RecipeVersions, RecipeIngredients, Users

**Production:** ShiftReports, ShiftWorkers, Rolls, RollTestReports, ThermoProductions, ThermoTestReports, ProducedBags, WoodenPallets, BagPalletAssignments, RecyclerProductions, PackagingMaterialConsumption

**Inventory:** Inventory, InventoryMovements

## 16. Entity Relationship Overview

```
Materials → RecipeIngredients → RecipeVersions → RecipeFamilies
                                       ↓
                                     Rolls
                                       ↓
                                RollTestReports
                                       ↓
                               ThermoProductions
                                       ↓
                              ThermoTestReports
                                       ↓
                                  ProducedBags
                                       ↓
                             BagPalletAssignments
                                       ↓
                                 WoodenPallets
                                       ↓
                                   Inventory
```

`ShiftReports` is the parent record for Rolls, Thermo Productions, Recycler Production, Packaging Material Consumption and Shift Workers.

`InventoryMovements` connect inventory with every production event.

## 17. Scalability

Future modules — Sales, Customers, Suppliers, Purchase Orders, Warehouse Locations, Multiple Warehouses, Machine Integration, Accounting, Shipment Tracking — none require redesigning the existing database.

## 18. Database Philosophy

Every production event references the previous event. Every inventory change is recorded. Every recipe is versioned. Every product receives a barcode. Historical data is preserved permanently.

---

## Open questions raised during review

See [open-questions.md](open-questions.md): **Q69** (six relationships declared 1:1 that cannot be), **Q70** (AuditLog missing from the table list), **Q71** (`UpdatedBy` contradicts immutability), **Q72** (table list confirms no withdrawal table exists), Q73–Q77.

---
*End of Part 9.*
