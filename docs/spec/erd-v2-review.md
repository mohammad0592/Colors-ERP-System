# ERD v2 — Schema as Drawn, and Review

Source: *Styrofoam Factory ERP — Database ERD (v2), "Based on New Information"*.

This supersedes the table sketches in Parts 2–9 wherever the two disagree. Below is the schema as drawn, then what changed against the written specification, then what the diagram resolves and what it leaves or introduces.

---

## 1. Schema as drawn

### Users & access
| Table | Columns |
|---|---|
| **Roles** | Id, Name |
| **Users** | Id, **RoleId (FK)**, FullName, Email, PasswordHash, IsActive |

### Recipe management
| Table | Columns |
|---|---|
| **RecipeFamilies** | Id, Name, ProductTypeId (FK), UsesRecycle (bool), Description |
| **RecipeVersions** | Id, RecipeFamilyId (FK), VersionNumber, Status, CreatedDate, CreatedByUserId (FK), **IsActive (bool)**, Notes |
| **RecipeIngredients** | Id, RecipeVersionId (FK), MaterialId (FK), **PercentageMin, TargetPercentage, PercentageMax** |
| **ProductTypes** | Id, Name |
| **Colors** | Id, Name |
| **PlateSizes** | Id, Name |

### Shifts
| Table | Columns |
|---|---|
| **Shifts** *(new)* | Id, Name, StartTime, EndTime |
| **ShiftReports** | Id, ShiftId (FK), ReportDate, OperatorId (FK), ElectricityStartMeter, ElectricityEndMeter, CycleTime, FeedDistance, MachineSpeed, ProductionStartTime, ProductionEndTime, **ActualProductionHours**, **DowntimeHours**, **RecyclerRunningWithThermo (bool)**, Notes |
| **ShiftWorkers** | Id, ShiftReportId (FK), UserId (FK), RoleInShift |

### Materials & inventory
| Table | Columns |
|---|---|
| **MaterialCategories** | Id, Name |
| **Units** | Id, Name, Symbol |
| **MovementTypes** *(new table)* | Id, Name |
| **Materials** | Id, Code, Name, CategoryId (FK), UnitId (FK), MinQuantity, **BarcodeTracked (bool)** |
| **MaterialInventory** | **MaterialId (PK/FK)**, CurrentQuantity, LastUpdated |
| **MaterialInventoryMovements** | Id, MaterialId (FK), MovementTypeId (FK), Quantity, ShiftReportId (FK), UserId (FK), Date, Notes |

### Extruder
| Table | Columns |
|---|---|
| **Rolls** | Id, RollCode, Barcode, RecipeVersionId (FK), ColorId (FK), ShiftReportId (FK), ProducedByUserId (FK), ProducedAt, Status — **Available / In Thermo / Consumed** |
| **RollTestReports** | Id, RollId (FK), Weight, Length, Thickness1–4, AverageThickness, Notes, TestedAt |

### Thermo
| Table | Columns |
|---|---|
| **ThermoProductions** | Id, ShiftReportId (FK), RollId (FK), OperatorId (FK), ProducedAt |
| **ThermoTestReports** | Id, ThermoProductionId (FK), TimeInMachine, PlateSizeId (FK), BagCount, PlateCount, PlateWeight, AbsorbentPercentage, BagWeight, Notes, TestedAt |
| **ThermoShiftSummary** *(new)* | Id, ShiftReportId (FK), LossPercentage, LossWeight, RollWeightUsed, TotalPlateCount, FinalProduct (text) |

### Bags, pallets, packaging, recycler
| Table | Columns |
|---|---|
| **ProducedBags** | Id, Barcode, ThermoProductionId (FK), ColorId (FK), ProductTypeId (FK), **PlateSizeId (FK)**, Weight, PlateCount, Status — Available / Assigned / **Defective**, CreatedAt |
| **BagPalletAssignments** | Id, BagId (FK), WoodenPalletId (FK), AssignedAt |
| **WoodenPallets** | Id, Barcode, ColorId (FK), ProductTypeId (FK), **BagCount, PlateCount**, Status — Building / Ready / Shipped, Notes, CreatedAt |
| **PackagingMaterialConsumption** | Id, ShiftReportId (FK), PlasticHoodCount, ShrinkCount, SmallBagCount, SmallBagWeight, BigBagCount, BigBagWeight, WoodenPalletCount, TapeCount |
| **RecyclerProduction** | Id, ShiftReportId (FK), ScrapWeight, RecycledMaterialWeight, LossPercentage, Notes |

### Recipe data confirmed on the diagram

| Id | Family | Uses recycle | Formula |
|---|---|---|---|
| 1 | Normal (Except Black) | No | GPPS 100% |
| 2 | Normal Black | Yes | GPPS 65% + Recycle 35% |
| 3 | ABS (Except Black) | No | GPPS 100% + Absorbent |
| 4 | ABS Black | Yes | GPPS 65% + Recycle 35% + Absorbent |

Worked example — Normal (Except Black) v1.0:

| Material | Target % | Min % | Max % |
|---|---|---|---|
| GPPS | 100 | 100 | 100 |
| Talc | 1 | 1 | 1 |
| Nucleating Agent | 1.8 | 1.5 | 2 |
| Coloring Agent | 1.6 | 1.5 | 2 |

> "Every change in percentages = New Version."

---

## 2. What the ERD resolves

| Was | Now | Entry |
|---|---|---|
| Polymorphic `Inventory` covering rolls, bags, pallets | `MaterialInventory` keyed by `MaterialId` — materials only; serialized items carry `Status` | **Q51, Q55, Q67** |
| No `Shifts` master table | `Shifts` with Name, StartTime, EndTime | **Q42** (partly) |
| Roll status gated on QC (`Waiting For Quality Test`) | `Available / In Thermo / Consumed` — quality no longer blocks thermo | **Q14** |
| Percentage basis ambiguous | Worked example shows GPPS at 100 with additives on top — parts per hundred resin, confirmed | **Q1** |
| `ProducedBags` had no plate size | `PlateSizeId` added | **Q32** (partly) |
| `Inventory.UnitId` duplicated `Materials.UnitId` | Gone | **Q60** |
| Movements repeated `ReferenceType`/`ReferenceId` | Gone | **Q58** |
| Movement type was a string | `MovementTypes` master table | **Q7-family** |
| `RecipeIngredients` min/max unclear | `PercentageMin` / `TargetPercentage` / `PercentageMax` explicit | — |
| Test reports had no timestamp | `TestedAt` on both | — |

---

## 3. What the ERD leaves unresolved

Unchanged and still open: **Q11 / A1** (movements carry `ShiftReportId` but no `RollId` or `RecipeVersionId`, so per-roll and per-recipe consumption remain uncomputable) · **Q2** (no link from `Colors` to a pigment `Material`) · **Q21** (bag/plate 500 divisibility) · **Q22** (`BagCount`, `PlateCount`, `BagWeight` still on the QC report) · **Q31** (packaging still fixed columns — see Q110) · **Q43** (no open/closed status on `ShiftReports`) · **Q53** (no sign convention — see Q106) · **Q54** (movements cannot name the causing event) · **Q65** (no `Barcodes` table) · **Q70** (no audit log table) · **Q78** (no supervisor role) · **Q36** (pallet capacity not stored as data).

---

## 4. New issues introduced by the ERD

Logged as **Q100–Q110** in [open-questions.md](open-questions.md):

- **Q100** — `Users` with a hand-rolled `PasswordHash` replaces ASP.NET Identity, which Part 10 §2 mandates.
- **Q101** — `Users.RoleId` is a single FK, so a person can hold exactly one role.
- **Q102** — `Templates` has been dropped from the schema entirely.
- **Q103** — `WoodenPallets.BagCount` / `.PlateCount` are stored, which Part 5 §10 explicitly forbade.
- **Q104** — `BagPalletAssignments` lost `AssignedByUserId`.
- **Q105** — `RecipeVersions` carries both `Status` and `IsActive`.
- **Q106** — `MovementTypes` has no direction, so movements have no sign.
- **Q107** — `Materials` lost `IsActive`; no master table has a soft-delete flag.
- **Q108** — `ThermoShiftSummary` stores derivable values and a free-text `FinalProduct`.
- **Q109** — `ShiftReports.OperatorId` replaces `SupervisorId`; `ActualProductionHours` is derived.
- **Q110** — packaging consumption cannot post to `MaterialInventoryMovements` without hardcoded mappings.
