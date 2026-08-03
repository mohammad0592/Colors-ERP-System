# Styrofoam Factory ERP System — Part 3: Production Module (Extruder Line)

Version: 1.0

## 1. Introduction

The Extruder Line is the first production stage. It converts raw materials into styrofoam rolls according to a selected recipe. Every roll in the factory starts here.

Responsibilities:
- Selecting the production recipe
- Recording raw material consumption
- Producing rolls
- Generating roll barcodes
- Recording roll production information
- Sending the roll to quality testing

Output: a produced roll.

## 2. Production Workflow

```
Receive Production Order
  ↓
Withdraw Raw Materials
  ↓
Select Recipe Version
  ↓
Mix Materials
  ↓
Extrusion Process
  ↓
Produce Roll
  ↓
Print Roll Barcode
  ↓
Create Roll Record
  ↓
Send Roll To Quality Testing
```

## 3. Material Withdrawal

Before production the operator withdraws materials from inventory, selecting Material, Quantity and Unit.

Example: GPPS 120 kg · Recycle Material 60 kg · Talc 1.2 kg · Coloring 2 kg · Absorbent Agent 3 kg.

Every withdrawal automatically reduces current inventory. The ERP must **prevent withdrawing more than exists** in inventory.

Every withdrawal records: Material, Quantity, User, Date, Shift Report, Notes.

## 4. Selecting the Recipe

The operator selects a **Recipe Version** (e.g. ABS Black → Version 1.2). The ERP displays ingredient percentages for reference.

The operator **cannot modify** the recipe. Recipe changes are performed only by administrators or authorized supervisors.

## 5. Creating a Roll

When production finishes a new roll is created. Every roll gets its own database record and is individually traceable.

## 6. Roll Identification

Every roll receives: unique database Id, Roll Code, Barcode, Production Date, Shift, Recipe Version, Color, Operator, Status.

The Roll Code is human-readable. Example: `13BABS240526A`

| Segment | Meaning |
|---|---|
| `13` | Production number |
| `B` | Black |
| `ABS` | Product type |
| `240526` | Production date |
| `A` | Shift |

The naming convention may be adjusted later if factory requirements change.

## 7. Barcode

Every roll receives a unique barcode immediately after production, printed and attached to the physical roll. Workers scan rather than type. The barcode is the roll's identity throughout the factory, later scanned by the thermo operator, quality control, the warehouse, and a future shipping module.

## 8. Roll Status

Suggested statuses: `Produced`, `Waiting For Quality Test`, `Ready For Thermo`, `In Thermo`, `Consumed`, `Archived`.

Status changes automatically based on factory operations.

## 9. Rolls Table

**Rolls**
- Id (PK)
- RollCode
- Barcode
- RecipeVersionId (FK → RecipeVersions)
- ColorId (FK → Colors)
- ShiftReportId (FK → ShiftReports)
- ProducedByUserId (FK → Users)
- Status
- ProducedAt
- Notes

> **Note:** Weight, Length and Thickness are **NOT** stored here — they belong to the Roll Test Report. The Rolls table stores identity and production information only.

## 10. Roll Test Reports

Documents the physical measurements taken after extrusion.

> The factory does **NOT** use these measurements to approve or reject production. They are stored for documentation and future analysis only. The roll always continues to production.

## 11. Roll Test Workflow

```
Quality Controller scans the roll barcode
  ↓
ERP loads roll information
  ↓
Operator enters measurements
  ↓
Report is saved
```

## 12. Roll Test Report Table

**RollTestReports**
- Id (PK)
- RollId (FK → Rolls)
- Weight
- ThicknessMeasurement1
- ThicknessMeasurement2
- ThicknessMeasurement3
- ThicknessMeasurement4
- Length
- AverageThickness
- Notes

`AverageThickness` is calculated automatically. The user enters only the four measurements.

Example: 2.45, 2.48, 2.50, 2.47 → average **2.475**

## 13. Relationship

```
Roll  1 ──── 1  RollTestReport
```

## 14. Traceability

Every roll permanently stores Recipe Version, Color, Shift, Operator, Production Date, Barcode and Roll Test Report — so years later the factory can answer which recipe produced this roll, who produced it, which shift, which colour, and which measurements were recorded.

## 15. Business Rules

- A roll must always reference exactly one Recipe Version.
- A roll can only have one Roll Test Report.
- A Roll Test Report cannot exist without a Roll.
- Roll measurements are recorded exactly once.
- Roll measurements are never edited after production except by authorized administrators.
- Every roll must have a unique barcode.
- Every roll must have a unique Roll Code.

## 16. Future Enhancements

Excluded from v1, but the database design must support: automatic barcode scanning from production machines, machine integration, automatic roll weight reading, IoT sensors, production scheduling, automatic recipe loading from PLC.

## 17. Module Summary

The Extruder module creates the first production object in the ERP. Everything later in the factory originates from the Roll — every Produced Bag and Finished Pallet traces back to it. The Roll is the central traceability entity of the manufacturing process.

---

## Open questions raised during review

See [open-questions.md](open-questions.md): Q11 (withdrawals are shift-level, not roll-level), Q12 (ShiftReports undefined but FK'd), Q13 (Color vs recipe family contradiction), Q14 (status chain blocks on a missing test), Q15 (Roll Code segment labelled product type is a recipe family), Q16–Q20.

---
*End of Part 3.*
