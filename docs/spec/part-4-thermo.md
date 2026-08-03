# Styrofoam Factory ERP System — Part 4: Thermo Production Module

Version: 1.0

## 1. Introduction

The second major production stage. Converts a finished styrofoam roll into packaged plate bags using a thermoforming machine.

Unlike the Extruder Line, the Thermo Line **consumes an existing roll**. The machine heats the roll and uses a template (mold) to produce styrofoam plates, which are automatically packed into bags. Each bag contains exactly 500 plates.

The ERP records the complete process and generates individually traceable bags.

## 2. Production Workflow

```
Scan Roll Barcode
  ↓
Load Roll Information
  ↓
Start Thermo Production
  ↓
Produce Plates
  ↓
Record Thermo Test Report
  ↓
Automatically Create Produced Bags
  ↓
Print Bag Barcodes
  ↓
Send Bags To Pallet Assembly
```

## 3. Roll Selection

The operator begins by scanning the roll barcode; the ERP retrieves roll information automatically.

The operator must **never** manually enter Recipe, Color, Product Type or Roll Number — these are inherited from the Roll.

## 4. Roll Consumption

One roll enters the machine and is processed **one time**. The factory does not split a roll across multiple thermo productions.

After production the roll status becomes `Consumed`. The roll remains in the database forever for traceability.

## 5. Thermo Production

A Thermo Production record represents the processing of one roll:

```
One Roll → One Thermo Production → One Thermo Test Report → Multiple Produced Bags
```

**ThermoProductions**
- Id (PK)
- RollId (FK → Rolls)
- ShiftReportId (FK → ShiftReports)
- OperatorId (FK → Users)
- StartedAt
- FinishedAt
- Notes

## 6. Thermo Test Reports

Documents production information recorded after the roll has been processed. Like the Roll Test Report it is **documentation only** — it does not approve or reject production.

## 7. Thermo Test Report Fields

Time Inside Machine · Plate Size · Plate Count · Bag Count · Plate Weight · Absorbent Percentage · Bag Weight · Notes

> **Not** entered here: Roll Weight, Roll Length, Roll Thickness — these already exist in the Roll Test Report and the ERP displays them automatically when needed.

## 8. Time Inside Machine

The ERP stores the duration the roll remained inside the machine (e.g. 28, 31, 35 minutes).

The ERP does **not** store Start Time or End Time — only the processing duration.

## 9. Plate Size

Every roll produces only one plate size: Large or Small. References the `PlateSizes` master table.

## 10. Plate Count

Total plates produced from the roll — e.g. 24,000. The ERP calculates it as **Bag Count × 500** when Bag Count is entered; the user should not calculate manually.

## 11. Bag Count

The number of **large bags** produced. Each large bag contains 500 plates. Example: 48 bags → 24,000 plates.

## 12. Plate Weight

Average weight of one plate, in **grams**. Example: 8.2 g.

## 13. Absorbent Percentage

Applicable only to absorbent products. The absorbent quality measured after production.

## 14. Bag Weight

Weight of one finished bag, in **kilograms**.

## 15. Thermo Test Reports Table

**ThermoTestReports**
- Id (PK)
- ThermoProductionId (FK → ThermoProductions)
- TimeInMachineMinutes
- PlateSizeId (FK → PlateSizes)
- PlateCount
- BagCount
- PlateWeight
- AbsorbentPercentage
- BagWeight
- Notes

```
ThermoProduction  1 ──── 1  ThermoTestReport
```

## 16. Produced Bags

After thermo production finishes the ERP **automatically** creates Produced Bags. Each gets its own database record, its own barcode, and is individually traceable.

## 17. Produced Bag Information

Unique ID · Barcode · Thermo Production · Color · Product Type · Weight · Plate Count · Status · Production Date

## 18. Produced Bags Table

**ProducedBags**
- Id (PK)
- Barcode
- ThermoProductionId (FK → ThermoProductions)
- ColorId (FK → Colors)
- ProductTypeId (FK → ProductTypes)
- Weight
- PlateCount
- Status
- ProducedAt
- Notes

## 19. Produced Bag Status

Suggested: `Produced`, `Waiting For Pallet`, `Assigned To Pallet`, `Stored`, `Shipped` (future).

Status changes automatically throughout production.

## 20. Barcode Generation

Immediately after creating a Produced Bag the ERP prints a barcode, attached to the physical bag. The packaging worker later scans it instead of typing bag information.

## 21. Traceability

Every Produced Bag knows which roll produced it, which recipe version produced that roll, which operator processed it, which shift produced it, and which thermo test report belongs to it.

## 22. Business Rules

- A Thermo Production always consumes exactly one Roll.
- A Roll can only be processed once.
- A Roll always creates one Thermo Production.
- A Thermo Production has exactly one Thermo Test Report.
- A Thermo Production produces one or more Produced Bags.
- Every Produced Bag has exactly one barcode.
- Barcodes must be unique.
- Bag Count must always equal Plate Count ÷ 500 — the ERP validates this automatically.

## 23. Future Enhancements

Outside v1, but supported by the design: automatic machine integration, automatic plate counting, automatic bag counting, machine sensor integration, automatic production statistics.

## 24. Module Summary

The Thermo Module converts Rolls into Produced Bags — loading roll information, recording thermo production and test reports, automatically creating bags, generating barcodes, and maintaining traceability:

```
Bag → Thermo Production → Roll → Recipe Version → Raw Materials
```

This completes the second manufacturing stage and prepares products for pallet assembly and packaging.

---

## Open questions raised during review

See [open-questions.md](open-questions.md): Q21 (the ÷500 rule blocks partial bags), Q22 (bag creation depends on a report owned by another role), Q23 (duration vs StartedAt/FinishedAt contradiction), Q24 (Template/mold never recorded), Q25 (failed rolls never reach thermo), Q26–Q30.

---
*End of Part 4.*
