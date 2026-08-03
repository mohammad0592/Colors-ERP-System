# Styrofoam Factory ERP System — Part 5: Packaging, Wooden Pallets and Barcode Traceability

Version: 1.0

## 1. Introduction

Begins after thermo production. At this stage the factory has finished bags of plates, each containing 500 plates, one product type, one colour, one barcode.

Purpose of the module:
- Organize produced bags into wooden pallets
- Track every bag using barcodes
- Consume packaging materials
- Record packaging statistics
- Prepare finished pallets for storage

Packaging does not produce plates — it organizes and prepares finished products for inventory.

## 2. Packaging Workflow

```
Produced Bags
  ↓
Create Wooden Pallet
  ↓
Print Pallet Barcode
  ↓
Scan Produced Bags
  ↓
Assign Bags To Pallet
  ↓
Pallet Becomes Full
  ↓
Consume Packaging Materials
  ↓
Move Finished Pallet To Inventory
```

## 3. Wooden Pallets

A wooden pallet is one finished inventory unit with its own identity: database record, barcode, status, product information, assigned bags.

**The pallet exists before any bags are assigned.**

## 4. Creating a Wooden Pallet

The packaging worker creates a new pallet. The ERP generates a unique pallet number, unique barcode, creation date, created by, status.

Initially: Bag Count = 0, Plate Count = 0.

## 5. Wooden Pallet Status

Suggested: `Building`, `Ready`, `Stored`, `Shipped` (future).

Initially `Building`; after reaching capacity, `Ready`.

## 6. Pallet Capacity

One wooden pallet normally contains **15 Produced Bags**.

All bags inside one pallet must share the same Product Type, Color, Plate Size, and Recipe (indirectly). The ERP prevents assigning incompatible bags.

- Allowed: 15 White Large Plate bags
- Not allowed: 10 White bags + 5 Black bags

## 7. Produced Bag Assignment

```
Scan Pallet Barcode
  ↓
Scan Bag Barcode
  ↓
ERP verifies compatibility
  ↓
Bag assigned
  ↓
Bag status updated
```

Repeated until the pallet is full.

## 8. Bag Assignment Rules

A Produced Bag belongs to **only one pallet**. Once assigned it cannot be assigned again. The ERP prevents duplicate assignments.

## 9. BagPalletAssignments Table

**BagPalletAssignments**
- Id (PK)
- ProducedBagId (FK → ProducedBags)
- WoodenPalletId (FK → WoodenPallets)
- AssignedByUserId (FK → Users)
- AssignedDate

## 10. WoodenPallets Table

**WoodenPallets**
- Id (PK)
- Barcode
- ProductTypeId (FK → ProductTypes)
- ColorId (FK → Colors)
- Status
- CreatedByUserId (FK → Users)
- CreatedDate
- Notes

> **Notice:** `BagCount` and `PlateCount` should **NOT** be permanently stored — they are always calculable:
> - Bag Count = `COUNT(BagPalletAssignments)`
> - Plate Count = `SUM(ProducedBag.PlateCount)`
>
> Avoid storing calculated values whenever possible.

## 11. Barcode Philosophy

Barcodes exist not only for identification but for **traceability, worker accountability, preventing manual errors, and forcing workers to use the ERP**. Every important production object receives its own barcode.

## 12. Objects That Receive Barcodes

Roll · Produced Bag · Wooden Pallet

Future: raw material packages, packaging material boxes, warehouse locations, machines.

## 13. Barcode Workflow

```
Roll → Thermo scans Roll Barcode → ERP loads Roll → Thermo produces Bags
  → ERP creates Bag Barcodes → Packaging scans Bag Barcode → Bag assigned to Pallet
  → ERP prints Pallet Barcode → Warehouse scans Pallet Barcode
```

Every production step begins with scanning. Manual typing is minimized.

## 14. Worker Accountability

Workers must not be able to bypass the ERP:
- Thermo cannot process a roll without scanning it.
- Packaging cannot assign a bag without scanning it.
- Warehouse cannot receive a pallet without scanning it.

Every action records User, Date, Time, Shift.

## 15. Packaging Materials

Packaging consumes Tape, Shrink Wrap, Plastic Hood, Large Bags, Small Bags, Wooden Pallets.

Unlike production materials these are recorded as **shift totals** — the factory does not currently measure partial consumption.

Example: Tape 3 rolls · Shrink 2 rolls · Small Bags 48 · Large Bags 24 · Empty Wooden Pallets 8.

## 16. Packaging Material Consumption

Recorded at the end of each shift; the worker records total quantities consumed and the ERP automatically decreases inventory.

**PackagingMaterialConsumption**
- Id (PK)
- ShiftReportId (FK → ShiftReports)
- TapeQuantity
- ShrinkQuantity
- PlasticHoodQuantity
- LargeBagQuantity
- SmallBagQuantity
- WoodenPalletQuantity
- RecordedByUserId (FK → Users)
- RecordedAt
- Notes

## 17. Inventory Effect

Saving packaging statistics automatically updates inventory (Tape: 20 in stock − 3 used = 17). The user never updates inventory manually.

## 18. Traceability

```
Pallet → Assigned Bags → Thermo Production → Roll → Recipe Version → Raw Materials
```

## 19. Business Rules

- One Produced Bag belongs to one Pallet only.
- One Pallet contains many Produced Bags.
- All bags inside one pallet must share Color, Product Type, Plate Size.
- A pallet cannot become `Ready` until it reaches the required capacity.
- Every Pallet has one Barcode; every Produced Bag has one Barcode.
- Every assignment is permanently stored.

## 20. Future Enhancements

Automatic barcode scanners, wireless handheld scanners, warehouse location tracking, shipping module, customer delivery tracking, automatic pallet weighing. Excluded from v1.

## 21. Module Summary

The Packaging Module transforms individual Produced Bags into finished inventory units, ensuring complete traceability, correct pallet composition, a barcode-based workflow, packaging material inventory control, and worker accountability.

---

## Open questions raised during review

See [open-questions.md](open-questions.md): Q31 (fixed material columns contradict the master-data pattern), Q32 (pallet has no PlateSizeId though §6 requires matching it), Q33 (pallet product/colour fixed before any bag exists), Q34 (empty wooden pallets double-counted), Q35 (no un-assign path after a mis-scan), Q36–Q41.

---
*End of Part 5.*
