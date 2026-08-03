# Styrofoam Factory ERP System — Part 6: Recycler Module and Shift Reporting

Version: 1.0

## 1. Introduction

Documents the recycling process. Unlike Extruder and Thermo, the Recycler does **not** process individual rolls — the factory collects all remaining scrap generated during the shift and weighs it once at shift end.

> The ERP follows the factory's real workflow rather than introducing unnecessary complexity.

Objectives:
- Record total scrap generated during the shift
- Record recycled material produced
- Calculate production loss
- Record shift statistics
- Return recycled material back into inventory

## 2. Current Factory Workflow

Leftover styrofoam scrap is collected during thermo production. The factory does **not** measure scrap per individual roll — all shift scrap is collected together.

At end of shift: total scrap is weighed → recycler processes it → recycled material is produced → stored in inventory.

## 3. Recycled Material

The recycler's output is **Recycled Material**, treated inside the factory as a raw material, used mainly in Black recipes (Normal Black and ABS Black: GPPS 65% / Recycle 35%).

After recycling the ERP automatically adds the produced recycled material back into inventory.

## 4. Recycler Workflow

```
Collect Scrap
  ↓
Weigh Total Scrap
  ↓
Process Scrap
  ↓
Produce Recycled Material
  ↓
Record Recycler Report
  ↓
Increase Inventory
```

## 5. Recycler Production Table

**RecyclerProductions**
- Id (PK)
- ShiftReportId (FK → ShiftReports)
- ScrapWeight
- ProducedRecycleWeight
- LossPercentage
- RecordedByUserId (FK → Users)
- RecordedAt
- Notes

## 6. Scrap Weight

Total weight of all collected scrap for one shift (e.g. 85 kg, 120 kg, 96 kg). Entered once.

## 7. Produced Recycled Material

Total weight of usable recycled material produced (e.g. 78 kg, 113 kg, 90 kg).

Immediately after saving, the ERP automatically increases inventory for the **Recycle** material.

## 8. Loss Percentage

Records production loss — scrap 100 kg, produced 95 kg → loss 5%. The ERP may calculate this automatically.

## 9. Shift Reports

One of the most important entities in the ERP. A Shift Report represents everything that happened during one production shift.

Rather than each production table storing a bare shift letter (A, B, C), they reference a specific Shift Report.

Example: Shift Report #152 = Shift A, 15/08/2026.

## 10. Shift Report Structure

**ShiftReports**
- Id (PK)
- ProductionDate
- Shift
- SupervisorId (FK → Users)
- ProductionStartTime
- ProductionEndTime
- MachineSpeed
- FeedDistance
- CycleTime
- ElectricityStartMeter
- ElectricityEndMeter
- Notes

## 11. Electricity Consumption

The factory records a start meter and an end meter. The ERP calculates:

```
Consumption = End Meter − Start Meter
```

> The calculated value should **not** be permanently stored.

## 12. Machine Settings

Recorded every shift: Machine Speed, Feed Distance, Cycle Time.

Valuable for later analysis — management can compare machine settings → quality → waste → production efficiency.

## 13. Shift Workers

A shift usually contains multiple workers, so storing a single operator is insufficient.

**ShiftWorkers**
- Id (PK)
- ShiftReportId (FK → ShiftReports)
- UserId (FK → Users)
- RoleDuringShift

Creates a many-to-many relationship between Users and Shift Reports.

## 14. Thermo Production During Shift

One shift may process multiple rolls (Shift A → Roll 1, Roll 2, Roll 3, Roll 4). Every Thermo Production references the Shift Report.

## 15. Packaging Material Consumption

Recorded once at end of shift — tape, shrink, plastic hood, large bags, small bags, wooden pallets. The ERP automatically deducts from inventory.

## 16. Shift Summary

Summary information recorded per shift:

- Total Roll Weight Used
- Total Scrap Weight
- Loss Percentage
- Produced Recycled Material
- Total Finished Bags
- Total Finished Pallets

Some values may be entered manually, others calculated automatically from existing production data.

## 17. Shift Report Relationships

```
One Shift Report
  ├── Many Thermo Productions
  ├── Many Shift Workers
  ├── One Packaging Material Consumption Record
  ├── One Recycler Production Record
  └── Many Inventory Movements
```

The Shift Report is the parent record for all activity during that shift.

## 18. Business Rules

- Every production activity belongs to exactly one Shift Report.
- Only one Recycler Production record per Shift Report.
- Only one Packaging Material Consumption record per Shift Report.
- A Shift Report cannot be deleted once production data has been recorded.
- Inventory updates generated from the Shift Report must occur automatically.

## 19. Future Enhancements

Automatic machine data collection, automatic electricity meter integration, real-time production dashboards, OEE, downtime analysis, machine alarms. Outside v1.

## 20. Module Summary

Provides a complete summary of factory activity per shift — machine settings, shift workers, packaging material consumption, recycler production, production statistics, electricity readings — and serves as the parent entity connecting the production modules.

---

## Open questions raised during review

Resolves [Q12](open-questions.md) in part — `ShiftReports` now exists, though its lifecycle is still undefined.

New: Q42 (`Shift` is a bare value, not a master table), Q43 (no open/closed lifecycle), Q44 (machine settings hardcoded and unattributed), Q45 (Shift Summary re-stores calculable values), Q46 (recycled material has no lot identity), Q47–Q50.

---
*End of Part 6.*
