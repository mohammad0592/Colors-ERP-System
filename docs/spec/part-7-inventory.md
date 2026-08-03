# Styrofoam Factory ERP System — Part 7: Inventory Management System

Version: 1.0

## 1. Introduction

Provides real-time information about everything the factory owns. Unlike traditional systems that manage only raw materials, this ERP manages every physical item:

Raw Materials · Packaging Materials · Recycled Materials · Produced Rolls · Produced Bags · Finished Wooden Pallets

Inventory is updated automatically by production processes. Users should rarely modify quantities manually.

## 2. Inventory Philosophy

The Inventory table represents the **current** quantity of every item — a dashboard.

| Item | Current Quantity |
|---|---|
| GPPS | 3,250 kg |
| Tape | 52 rolls |
| Produced Rolls | 18 rolls |
| Finished Pallets | 145 pallets |

It stores only the latest quantity and **no history**. History lives in Inventory Movements.

## 3. Why Separate Inventory and Inventory Movements?

If inventory only stored `GPPS = 500 kg` and a withdrawal of 50 kg simply set it to 450 kg, the history is lost — who withdrew it, when, why, from which shift.

Therefore every inventory change creates an Inventory Movement. **Inventory = current balance. InventoryMovements = complete history.**

## 4. Inventory Table

**Inventory**
- Id (PK)
- ReferenceType
- ReferenceId
- CurrentQuantity
- UnitId (FK → Units)
- LastUpdated
- Notes

`ReferenceType` determines the kind of item: `Material`, `Roll`, `ProducedBag`, `WoodenPallet`. `ReferenceId` points to the corresponding table.

## 5. Understanding ReferenceType and ReferenceId

One Inventory table manages all physical assets without separate tables per type:

- `ReferenceType = Material`, `ReferenceId = 5` → Material #5
- `ReferenceType = Roll`, `ReferenceId = 120` → Roll #120
- `ReferenceType = ProducedBag`, `ReferenceId = 810` → Produced Bag #810

## 6. Inventory Movement Table

**InventoryMovements**
- Id (PK)
- InventoryId (FK → Inventory)
- MovementType
- Quantity
- ReferenceType
- ReferenceId
- ShiftReportId (FK → ShiftReports)
- UserId (FK → Users)
- MovementDate
- Notes

## 7. Movement Types

`Receive` · `Production` · `Consumption` · `Transfer` · `Adjustment` · `Recycle` · `Packaging`

Each movement explains why inventory changed.

## 8. Automatic Inventory Updates

| Event | Effect |
|---|---|
| Receiving raw materials | Inventory increases |
| Extruder withdraws GPPS | Inventory decreases |
| Recycler produces recycled material | Inventory increases |
| Packaging consumes tape | Inventory decreases |
| Thermo produces bags | Produced Bag inventory increases |
| Finished pallet stored | Wooden Pallet inventory increases |

## 9. Inventory Adjustment

Physical inventory may differ from the ERP — counting mistakes, damage, loss. An Administrator performs an Inventory Adjustment, which creates an Inventory Movement. The previous quantity is never overwritten without history.

## 10. Receiving Materials

The administrator selects Material, Quantity, Supplier (future), Date, Notes. The ERP creates a `Receive` movement and updates inventory.

Purchasing is not included yet.

## 11. Material Consumption

Occurs during Extruder production (GPPS 120 kg, Recycle 60 kg, Talc 1.2 kg, Coloring 2 kg).

Each withdrawal creates an Inventory Movement, updates current inventory, links to the Shift Report and to the User.

## 12. Packaging Material Consumption

Consumed at end of shift — tape, shrink, plastic hood, large bags, small bags, wooden pallets. The worker enters totals; the ERP updates inventory.

## 13. Produced Roll Inventory

Roll produced → inventory increases. Thermo consumes the roll → inventory decreases. No manual updates.

## 14. Produced Bag Inventory

After thermo production, bag inventory increases. When assigned to a pallet the bag still exists — it changes status. When shipped (future), inventory decreases.

## 15. Wooden Pallet Inventory

After a pallet becomes `Ready`, inventory increases. It remains until shipped (future module).

## 16. Inventory Relationships

```
Inventory  1 ──── ∞  InventoryMovements
```

Inventory references Materials, Rolls, Produced Bags, Wooden Pallets.
Inventory Movements reference Users, Shift Reports, Production Modules.

## 17. Business Rules

- Inventory quantities must never become negative.
- Every inventory change creates an Inventory Movement.
- Users cannot edit inventory quantities directly.
- Only administrators may perform inventory adjustments.
- Every movement stores Date, Time, User, Reason, Shift.
- Inventory is updated automatically by production modules whenever possible.

## 18. Reporting

Current Material Stock · Materials Below Minimum Quantity · Roll Inventory · Produced Bag Inventory · Finished Pallet Inventory · Inventory Movement History · Consumption By Shift · **Consumption By Recipe** · **Production Yield**

## 19. Future Enhancements

Warehouse locations · multiple warehouses · batch tracking · supplier integration · purchase orders · sales orders · FIFO/LIFO costing · expiration tracking · barcode warehouse scanning.

## 20. Module Summary

Manages every physical item in the factory, separating **current inventory** from **inventory history** to provide real-time stock levels, complete audit history, automatic updates and full production traceability.

---

## Open questions raised during review

See [open-questions.md](open-questions.md): **Q51** (serialized items duplicate their own status columns), **Q52** (§18 requires two reports that Q11 makes impossible), Q53 (movement sign is undefined), Q54 (movements cannot name the transaction that caused them), Q55 (bags and their pallet are both counted), Q56–Q60.

---
*End of Part 7.*
