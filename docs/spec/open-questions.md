# Open Questions & Design Tensions

Accumulated while reading the specification. Nothing here blocks further parts — several may be answered by parts not yet received.

Status: `open` | `answered` | `deferred`

---

## ERD v2 status update

The v2 ERD ([review](erd-v2-review.md)) supersedes the table sketches in Parts 2–9. It **resolves**:

- **Q51, Q55, Q67** — inventory is now `MaterialInventory` keyed by `MaterialId`; rolls, bags and pallets carry `Status` instead of inventory rows. Exactly the split proposed.
- **Q14** — roll status is `Available / In Thermo / Consumed`. No QC gate, so quality genuinely never blocks thermo.
- **Q1** — the worked example (GPPS target 100/100/100, additives 1–2%) confirms parts per hundred resin. A basis marker is still worth adding so validation knows GPPS + Recycle must total 100 while additives sit on top.
- **Q60, Q58** — `Inventory.UnitId` and the duplicated `ReferenceType`/`ReferenceId` are gone.
- **Q42** *(partly)* — `Shifts` (Name, StartTime, EndTime) now exists. Still needed: a unique constraint on (`ShiftId`, `ReportDate`), and the rule for which date a night shift belongs to.
- **Q32** *(partly)* — `ProducedBags.PlateSizeId` added. `WoodenPallets` still has no `PlateSizeId`, so the same-plate-size rule for a pallet is still not checkable on the pallet itself.

Still open and unchanged: **Q11/A1**, Q2, Q21, Q22, Q31, Q36, Q43, Q53, Q54, Q65, Q70, Q78.

New from the ERD: **Q100–Q110** at the end of this file.

---

## Q1 — Percentage basis in RecipeIngredients
**Source:** Part 2 §12, §13 · **Status:** open · **Impact:** high

The Family 1 recipe reads GPPS 100% + Talc 1% + Nucleating 1.5–2% + Coloring 1.5–2%, which sums to ~104%. Family 2 reads GPPS 65% + Recycle 35% (=100% polymer) plus the same additives on top.

This is the standard *parts per hundred resin* convention: the polymer total is the 100% base and additives are expressed relative to it — not as a share of batch weight. If the schema treats `TargetPercentage` as "% of total batch", every validation and every kg calculation at the extruder will be wrong.

Two candidate fixes:
- Add `BasisType` to `RecipeIngredients` (`PolymerBase` | `Additive`), validate that `PolymerBase` rows sum to 100%.
- Or add `IsBaseResin` (bool) with the same effect.

**Needs from you:** confirmation that additives are phr, and whether the extruder enters a batch size in kg of resin (Part 3 territory).

---

## Q2 — Generic "Coloring Agent" vs. the Colors table
**Source:** Part 1 §3.1, Part 2 §4, §6, §13 · **Status:** open · **Impact:** high

Families 1 and 3 are "Except Black" and list a generic `Coloring Agent` ingredient, while a Roll carries a specific `Color` (White, Blue, Green…). So one recipe version serves many colors, and the *actual* pigment material withdrawn from inventory is chosen at production time — but the schema has no link between `Colors` and `Materials`.

Without that link the system cannot deduct the right pigment from inventory, and backward traceability from a roll cannot name which pigment went in.

Candidate fixes:
- `Materials.ColorId` (nullable) marking pigment materials, plus a rule that the extruder picks a pigment matching the roll's colour.
- Or a `ColorMaterials` mapping table (Color → Material), allowing more than one pigment per colour.
- Or the extruder production record stores the resolved `MaterialId` per ingredient line (which also handles substitutions generally).

**Needs from you:** how the operator currently chooses the pigment, and whether one colour ever maps to more than one pigment material.

---

## Q3 — Templates vs. PlateSizes overlap
**Source:** Part 2 §7, §8 · **Status:** open · **Impact:** medium

`Templates` are "Large Plate" / "Small Plate" and `PlateSizes` are "Large" / "Small". A template already implies both a product type and a size, so recording both on a thermo production allows a contradiction (template "Small Plate" + plate size "Large").

Candidate fix: `Templates.PlateSizeId` (nullable, since a meal-box mold has no plate size), and derive the plate size from the template rather than entering it separately.

**Needs from you:** does the thermo operator actually pick a mold, a size, or both?

---

## Q4 — RecipeFamilies.UsesRecycleMaterial can go stale
**Source:** Part 2 §10, §12 · **Status:** open · **Impact:** low

The flag lives on the family, but ingredients live on the version. A new version could add or remove Recycle and the family flag would then be wrong. It is derivable from the version's ingredients.

Candidate fix: drop the column and derive it, or move it to `RecipeVersions` where the ingredients actually live. Keep it only if it is needed purely as a UI filter and drift is acceptable.

---

## Q5 — Traceability terminates at recycled material
**Source:** Part 1 §3.3, §5.4 · **Status:** open · **Impact:** medium (inherent to the process)

§5.4 requires every product to be traceable back to raw materials, but scrap is weighed once per shift, not per roll. Once a Black recipe consumes Recycled Material, the backward trace resolves only to "recycled lot from shift X" — never to the rolls that produced that scrap.

This is physical, not a schema flaw. The schema should make the boundary explicit by keying recycled lots to a shift, and reporting should state the limit rather than implying a complete chain.

---

## Q6 — Enforcing "one Current version per family"
**Source:** Part 2 §11 · **Status:** answered by Part 12 §6 · **Impact:** low (implementation detail)

> **Update (Part 12 §6):** "Only one version is active for production at any given time" — firmer than Part 2's "normally", so this is a hard rule. The partial unique index below is the right enforcement, and promoting a new version must demote the previous one to `Archived` in the same transaction.

"Normally only one" needs to be either enforced or not. In PostgreSQL a partial unique index does it cleanly:

```sql
CREATE UNIQUE INDEX ux_recipe_current
  ON "RecipeVersions" ("RecipeFamilyId")
  WHERE "Status" = 'Current';
```

**Needs from you:** is "normally" a hard rule, or are there legitimate periods with two Current versions?

---

## Q7 — When exactly does a RecipeVersion become immutable?
**Source:** Part 2 §11 · **Status:** open · **Impact:** medium

Part 1 says recipe versions are never edited; Part 2 says a version becomes immutable "once used in production" and allows a `Draft` status. Those imply different rules — a `Draft` is editable, and a `Current` version with no production yet is ambiguous.

Suggested rule: editable only while `Draft`; promoting to `Current` freezes it permanently. That is simpler to enforce and to explain than "editable until first roll".

---

## Q8 — VersionNumber data type
**Source:** Part 2 §11, §14 · **Status:** open · **Impact:** low

Examples use "1.0" and "1.1". Stored as a decimal, 1.10 and 1.1 collide and sorting past 1.9 → 1.10 misbehaves. Suggest either an integer sequence per family (1, 2, 3…) displayed however you like, or separate `Major`/`Minor` integer columns.

---

## Q9 — Master data not yet specified
**Source:** Part 1 §5.4, §3 · **Status:** deferred — likely Part 3

Referenced by Part 1 but absent from Part 2's master data: **Shifts**, **Machines** (Part 1 §5.4 asks "which thermo machine processed it"), and **Warehouses / storage locations** for inventory. Assuming these arrive with the production or inventory module.

> **Update (Part 6):** still all three. Shifts are stored as a bare value on `ShiftReports` rather than a master table (**Q42**); machines remain unmodelled even though Part 6 §10 records machine settings (**Q44**, **Q20**); locations remain deferred (**Q41**).

---

## Q10 — "Recycled Material" as both a category and a material
**Source:** Part 1 §5.2, Part 2 §3, §4 · **Status:** open · **Impact:** low

`MaterialCategories` includes "Recycled Material" while `Materials` lists "Recycle" among raw materials. Needs a single answer: is Recycle a Raw Material, or the sole member of its own category? Reporting groups will differ.

---

## Q11 — Material withdrawals are shift-level, so no roll consumes known materials
**Source:** Part 3 §3, §17; Part 1 §5.4 · **Status:** open · **Impact:** high

Part 3 §3 records a withdrawal against **Material, Quantity, User, Date, Shift Report, Notes** — there is no `RollId`. But §17 states every bag and pallet traces back to the roll, and Part 1 §5.4 requires tracing back to the raw materials used.

With withdrawals keyed to a shift report, the strongest available answer is "these materials were withdrawn during the shift that produced this roll" — not "this roll consumed these materials". If a shift produces eight rolls, all eight share one undifferentiated material pool. That is weaker than the traceability the spec promises, and it also prevents any per-roll yield or waste calculation.

Three options, in increasing cost to the operator:
- **Keep shift-level** (cheapest, matches how the operator actually works) and state the limit explicitly in reporting.
- **Allocate proportionally** — split shift withdrawals across the shift's rolls by roll weight from the test report. Derived, not measured; adequate for cost reporting, misleading for defect investigation.
- **Withdraw per roll** — add nullable `RollId` to the withdrawal so a batch mixed for one roll is attributed to it.

**Needs from you:** does the operator mix per roll, or mix a batch that yields several rolls? That single fact decides this.

---

## Q12 — ShiftReports is FK'd but never defined
**Source:** Part 3 §3, §9 · **Status:** mostly answered by Part 6 §9–§10 · **Impact:** high (blocking for this module)

> **Update (Part 6):** `ShiftReports` is now defined, with production date, shift, supervisor, start/end times, machine settings and electricity meters. Two of the original unknowns remain open and have moved to their own entries: shift definitions and boundaries → **Q42**; who opens and closes a report, and whether production can post to a closed one → **Q43**. The "one report per factory or per line" question is answered — Part 6 §17 makes it factory-wide.

`Rolls.ShiftReportId` is a foreign key and withdrawals reference a Shift Report, but no `ShiftReports` table appears in Part 2's master data or in Part 3. Part 1 §3.3 also puts recycler output at shift level, so this entity carries real weight across three modules.

Still unknown: shift definitions (A/B/C, start and end times, night shifts crossing midnight), who opens and closes a shift report, whether one report covers the whole factory or one per production line, and whether production can be recorded against a closed shift.

Assuming this arrives in a later part. If it does not, it needs specifying before the extruder module can be built. Supersedes the "Shifts" half of Q9.

---

## Q13 — Colour and recipe family can contradict each other
**Source:** Part 3 §9; Part 2 §10, §13 · **Status:** open · **Impact:** medium

A roll stores `ColorId`, and separately a `RecipeVersionId` whose family is either "…Black" or "…(Except Black)". Nothing stops a roll from recording recipe family *Normal (Except Black)* with `ColorId = Black`, or *Normal Black* with `ColorId = White`. Both are physically impossible.

Candidate fix: an `IsBlackOnly` / `AllowedColorScope` flag on `RecipeFamilies`, validated on roll creation. Closely related to Q2 — if colour resolves to a pigment material, that mapping can carry the same constraint.

---

## Q14 — The status chain blocks thermo on a *missing* quality test
**Source:** Part 3 §8, §10; Part 1 §3.1 · **Status:** open · **Impact:** medium

The status sequence is `Produced → Waiting For Quality Test → Ready For Thermo`, which means a roll cannot reach thermo until QC has entered a report. But Part 1 §3.1 and Part 3 §10 both insist quality never blocks production and the roll always continues.

Failing a test and having no test yet are different situations, and the spec only resolves the first. If QC is busy or absent at shift end, does the roll wait?

**Needs from you:** can the thermo operator consume a roll that has no test report — yes (log a warning) or no (hard block)? This also decides whether `RollTestReports` is genuinely 1:1 or 1:0..1 — see Q16.

---

## Q15 — Roll Code segment labelled "product type" is a recipe family
**Source:** Part 3 §6; Part 2 §5, §9 · **Status:** open · **Impact:** low

In `13BABS240526A` the `ABS` segment is documented as the product type, but Part 2 defines product types as Plate / Meal Box / Container, and ABS as a *recipe family*. The code encodes the family, not the product type.

Naming only — but the generator must read from the right table. Two further gaps in the format: the scope of the `13` production number (per day, per shift, or continuous — it determines collision risk), and what occupies the `B` position for non-black rolls.

---

## Q16 — Roll ↔ RollTestReport is 1:1 in §13 but optional in practice
**Source:** Part 3 §10, §13, §15 · **Status:** open · **Impact:** low

§13 shows a strict 1:1. But a report is only created after production, so a roll necessarily exists without one for some period — and if Q14 resolves toward "thermo may proceed untested", a roll may never get one. The real relationship is **Roll 1 ── 0..1 RollTestReport**, enforced by a unique constraint on `RollId`.

---

## Q17 — "Never edited except by administrators" needs an audit trail
**Source:** Part 3 §15; Part 1 §9 · **Status:** open · **Impact:** medium

Part 1 makes production records immutable; Part 3 §15 permits authorized administrators to edit roll measurements. An in-place `UPDATE` silently destroys the original reading, which defeats the purpose of keeping the record.

Suggested: keep the row immutable and record corrections in a `RollTestReportRevisions` history table capturing old value, new value, user, timestamp and reason. Reports then show the current value with an "amended" marker.

**Needs from you:** confirmation that admin corrections are expected to be rare (typo fixes) rather than routine.

---

## Q18 — Withdrawal unit vs. the material's own unit
**Source:** Part 3 §3; Part 2 §4 · **Status:** open · **Impact:** low

The withdrawal screen offers Material, Quantity **and Unit**, but `Materials` already carries a `UnitId` and no conversion factors exist anywhere in the schema. Allowing a unit different from the material's own would require a conversion table that has not been specified.

Simplest resolution: the withdrawal always uses the material's own unit, and the UI displays it read-only rather than offering a choice.

---

## Q19 — Inventory balance table and concurrent withdrawals
**Source:** Part 3 §3 · **Status:** deferred — likely the inventory part · **Impact:** medium

§3 requires that a withdrawal reduce "current inventory quantity" and that over-withdrawal be prevented, which implies a stored balance (or a running sum over movements) that has not yet been specified.

Two operators withdrawing the same material at once can both pass the availability check and drive stock negative. This needs either row-level locking on the balance row or a `CHECK (quantity >= 0)` constraint with retry — a decision to make when the inventory module is specified, noted here so it is not missed.

---

## Q20 — Extruder machine not recorded on the roll
**Source:** Part 3 §9; Part 1 §3, §5.4 · **Status:** open · **Impact:** low

Part 1 §5.4 asks "which thermo machine processed it", so machines matter for traceability, but `Rolls` records no extruder or line. Part 1 describes a single extruder line, so this may be intentional.

**Needs from you:** is there ever more than one extruder? If a second line is plausible, a nullable `MachineId` now is far cheaper than a backfill later.

---

## Q21 — The ÷500 rule cannot hold for a real roll
**Source:** Part 4 §1, §10, §11, §18, §22 · **Status:** open · **Impact:** high

§22 makes it a validated business rule that **Bag Count = Plate Count ÷ 500**, and §10 has the ERP *derive* plate count as Bag Count × 500. That only works if every roll yields an exact multiple of 500 plates. A roll runs out when it runs out — the last bag will normally be partial.

Enforced as written, the rule forces the operator to either discard the remainder or falsify the count to pass validation. Both defeat the purpose of the record, and the second is exactly the behaviour §5.3 of Part 1 is trying to eliminate.

The schema already hints the factory knows this: `ProducedBags.PlateCount` is stored **per bag** (§18), which is redundant if every bag holds exactly 500.

Suggested shape: treat 500 as the *standard* bag quantity, allow a final partial bag, and validate `PlateCount = (FullBags × 500) + Remainder` rather than requiring exact divisibility. Plate count becomes the entered value, bag count the derived one.

**Needs from you:** what physically happens to the leftover plates at the end of a roll — partial bag, held over to the next roll, or scrapped into the recycler? This decides the whole calculation.

---

## Q22 — Bag creation depends on a report owned by a different role
**Source:** Part 4 §2, §15, §16; Part 1 §7 · **Status:** open · **Impact:** high

`BagCount`, `PlateCount` and `BagWeight` live on **ThermoTestReports**, but those are the values needed to create the Produced Bags — and the workflow in §2 puts "Record Thermo Test Report" *before* "Automatically Create Produced Bags".

Part 1 §7 assigns those two steps to different people: the **Thermo Operator** creates produced bags and prints barcodes; the **Thermo Quality Control** records thermo test reports. So the operator cannot produce bags or barcodes until QC has filled in a form, even though §6 calls that form documentation with no effect on production.

This also means bags physically exist on the floor with no barcode and no database record whenever QC lags — the untracked gap the ERP is meant to close.

The underlying issue is classification: `BagCount`, `PlateCount` and `BagWeight` are **production quantities**, not quality measurements. `TimeInMachineMinutes`, `PlateWeight` and `AbsorbentPercentage` are genuine measurements.

Suggested fix: move the three quantity fields to `ThermoProductions` (operator-owned, drives bag creation) and leave the measurements on `ThermoTestReports` (QC-owned, documentation only). Bags can then be created the moment production ends, and QC's report stays genuinely optional — consistent with §6 and with Q14.

**Needs from you:** confirmation that the operator, not QC, is the one who knows the bag count at the end of a run.

---

## Q23 — Duration and timestamps contradict each other
**Source:** Part 4 §5, §8 · **Status:** open · **Impact:** low

§8 states plainly that the ERP does **not** store start or end time, only the duration. But §5 gives `ThermoProductions` both `StartedAt` and `FinishedAt`, from which duration is derivable — and which can disagree with the separately entered `TimeInMachineMinutes`.

These are probably measuring different things: `StartedAt`/`FinishedAt` bound the whole production run, while "time inside machine" is the heating dwell time for the roll. If so both are legitimate and the field just needs a clearer name (`DwellTimeMinutes`). If they mean the same thing, one must go.

**Needs from you:** is "time inside machine" the same span as start-to-finish of the run, or a shorter dwell time within it?

---

## Q24 — The mold (Template) is never recorded on a production
**Source:** Part 4 §1, §9, §15; Part 2 §8 · **Status:** open · **Impact:** medium

§1 says the machine "uses a template (mold) to produce styrofoam plates", and Part 2 §8 defines `Templates` as exactly that, tied to a `ProductTypeId`. But no table in Part 4 carries a `TemplateId` — `ThermoTestReports` records `PlateSizeId` instead.

Two consequences. The `Templates` table is currently orphaned — nothing in the specification references it, so it would ship unused. And the product type of a bag (§18 `ProductTypeId`) has no source: it cannot come from the mold, because the mold is not recorded, so it must be inferred from the recipe family — which is a *recipe* classification, not a product one (see Q15).

Recording `TemplateId` on `ThermoProductions` fixes both: it identifies the physical mold for traceability, and `Template → ProductTypeId` gives the bag's product type an authoritative source. It also resolves Q3, since plate size can hang off the template rather than being entered separately.

**Needs from you:** does the factory swap molds often enough to want the specific mold on the record, or is "Large Plate / Small Plate" sufficient?

---

## Q25 — "A Roll always creates one Thermo Production" excludes failed rolls
**Source:** Part 4 §22; Part 1 §3.1; Part 3 §8 · **Status:** open · **Impact:** medium

§22 states a roll always creates one thermo production. But Part 1 §3.1 says failed rolls "may later be donated, used as samples, or handled manually" — none of which is thermo production — and Part 3 §8 gives rolls an `Archived` status for exactly that outcome.

So the true relationship is **Roll 1 ── 0..1 ThermoProduction**: at most one, enforced by a unique constraint on `RollId`, never mandatory.

This also raises an unhandled case: a roll that is donated or sampled leaves inventory without being consumed by thermo. Nothing yet records that disposal, its reason, or its quantity — so those rolls would sit in `Ready For Thermo` forever. A disposal record (roll, reason, user, date) would close it.

---

## Q26 — ProducedBags duplicates colour and product type
**Source:** Part 4 §18 · **Status:** open · **Impact:** low

`ColorId` and `ProductTypeId` on `ProducedBags` are both reachable by joining Bag → ThermoProduction → Roll → RecipeVersion → RecipeFamily. Storing them again allows a bag to contradict the roll it came from.

This is defensible as a deliberate snapshot — it keeps bag labels and queries cheap, and matches the immutability philosophy. It just needs to be *stated* as denormalization, populated once at creation from the roll, and never editable independently. Otherwise the two will drift and no one will know which is authoritative.

---

## Q27 — Per-bag weight is copied, not measured
**Source:** Part 4 §14, §18 · **Status:** open · **Impact:** low

`ThermoTestReports.BagWeight` is a single measurement of "one finished bag", but `ProducedBags.Weight` exists on every bag. In practice all bags from one production would receive the same copied nominal value.

Worth being explicit that this is a nominal weight, not per-bag measured, so nobody later builds a yield or shrinkage report on it believing otherwise. If bags are ever weighed individually, this becomes a real per-bag field and the report value becomes an average.

There is also a free consistency check available: `PlateWeight × PlateCount per bag ≈ BagWeight` (8.2 g × 500 ≈ 4.1 kg). Worth surfacing as a soft warning on entry, not a hard block.

---

## Q28 — Small Bags are never used anywhere
**Source:** Part 4 §11; Part 1 §5.2 · **Status:** open · **Impact:** medium

§11 defines bag count as the number of **large** bags, and every bag holds 500 plates. But Part 1 §5.2 lists both **Large Bags** and **Small Bags** as packaging materials to be tracked.

Nothing in the specification so far consumes a small bag. Either they package something not yet described (a partial remainder — see Q21 — or a different product), or they are a second packaging size for the same plates.

**Needs from you:** what goes into a small bag, and how many plates does it hold?

---

## Q29 — No packaging material is deducted from inventory
**Source:** Part 4 §16; Part 1 §5.2 · **Status:** deferred — likely the packaging part · **Impact:** medium

Creating 48 produced bags consumes 48 large bags from inventory, but nothing in Part 4 records that consumption. The same applies to tape, shrink wrap, plastic hoods and wooden pallets at the packaging stage.

Part 1 §5.2 requires these to be tracked, so either the packaging module deducts them or stock levels will only ever go up on receipt and never down. Assuming a later part covers it; noted so it is not lost. Related to Q19.

---

## Q30 — AbsorbentPercentage applies to only some recipes
**Source:** Part 4 §13, §15; Part 2 §13 · **Status:** open · **Impact:** low

"Only applicable to absorbent products" means the column is nullable, and that validation should require a value only when the roll's recipe family is an ABS family (families 3 and 4 are the ones carrying Absorbent Agent).

That rule needs the report to reach the recipe family through Production → Roll → RecipeVersion → RecipeFamily, which works, but implies an `IsAbsorbent` flag on `RecipeFamilies` rather than matching on the family *name* — names must never drive logic (Part 2 §4 makes the same point about materials).

---

## Q31 — PackagingMaterialConsumption hardcodes one column per material
**Source:** Part 5 §16; Part 2 §4, §12 · **Status:** open · **Impact:** high

`PackagingMaterialConsumption` has fixed columns — `TapeQuantity`, `ShrinkQuantity`, `PlasticHoodQuantity`, `LargeBagQuantity`, `SmallBagQuantity`, `WoodenPalletQuantity`. Adding a seventh packaging material then requires a schema migration, an entity change, a DTO change and a UI change.

This contradicts two decisions the specification has already made. Part 2 §4 stores every material as a row in `Materials` precisely so materials are data, not schema. Part 2 §12 explicitly rejected this same shape for recipes: *"Instead of storing ingredient percentages directly inside RecipeVersions, a separate RecipeIngredients table is used. This allows unlimited ingredients."* The identical reasoning applies here.

It also breaks the "never depend on the material name" rule — these columns bind material identity into column names, so a rename or a second tape type has nowhere to go.

Suggested shape, mirroring `RecipeIngredients`:

```
PackagingMaterialConsumptions        PackagingMaterialConsumptionLines
  Id (PK)                              Id (PK)
  ShiftReportId (FK)                   ConsumptionId (FK)
  RecordedByUserId (FK)                MaterialId (FK → Materials)
  RecordedAt                           Quantity
  Notes                                (unique on ConsumptionId + MaterialId)
```

The entry screen then lists every material in the `Packaging Material` category automatically — new materials appear with no code change.

---

## Q32 — Pallets must match plate size, but nothing stores it
**Source:** Part 5 §6, §10, §19; Part 4 §15, §18 · **Status:** open · **Impact:** high

§6 and §19 both require every bag on a pallet to share the same **Plate Size**. But `WoodenPallets` (§10) has only `ProductTypeId` and `ColorId`, and `ProducedBags` (Part 4 §18) has no plate size either — it lives on `ThermoTestReports.PlateSizeId`.

So validating the rule means joining Bag → ThermoProduction → ThermoTestReport. That has two consequences:

1. If the QC report has not been entered yet, the plate size is **unknown** and the rule cannot be evaluated at all — packaging is blocked by QC, exactly as in Q22.
2. `ProducedBags` already denormalizes `ColorId` and `ProductTypeId` (Q26). Plate size is the one attribute of the same kind that was left out, which looks like an oversight rather than a decision.

Suggested fix: add `PlateSizeId` to `ProducedBags`, copied at creation like the other two, and `PlateSizeId` to `WoodenPallets` so the pallet's constraint is explicit and checkable with one comparison. This depends on Q24 — if the mold is recorded on the production, plate size comes from the template and is known before QC touches anything.

---

## Q33 — A pallet's product type and colour are fixed before any bag exists
**Source:** Part 5 §3, §4, §10 · **Status:** open · **Impact:** medium

§3 and §4 are explicit that the pallet exists before any bags are assigned, created with Bag Count = 0. Yet §10 gives `WoodenPallets` non-obviously-nullable `ProductTypeId` and `ColorId`. Nothing says where those values come from at creation time.

Two coherent readings, and they produce different screens:

- **Declared up front** — the worker chooses "White / Large Plate" when creating the pallet, and it becomes the constraint every scanned bag is checked against. Catches a wrong bag on the first scan.
- **Inherited from the first bag** — the columns are nullable until the first assignment, then locked. Fewer taps, but the first scan can never be wrong, so a mis-scan silently defines the whole pallet.

The first is better for the accountability goal in §14 and only costs one screen. Either way the columns' nullability and the locking rule need stating.

---

## Q34 — Empty wooden pallets are counted twice
**Source:** Part 5 §4, §15, §16 · **Status:** open · **Impact:** medium

Every `WoodenPallets` row is one physical empty pallet taken from inventory, so the system already knows exactly how many were used in a shift — `COUNT(*)` of pallets created. But §16 also has the worker type a shift total into `WoodenPalletQuantity`, and §17 deducts *that* number from inventory.

The two figures will disagree. Worse, only the typed one moves stock, so the authoritative record (actual pallet rows) has no inventory effect while a hand-keyed number does.

Suggested fix: deduct one Empty Wooden Pallet at pallet creation and drop the field from the shift form. The same logic applies to **Large Bags**, which are consumed one per produced bag and are likewise exactly countable (Q29) — the shift form should only carry materials the factory genuinely cannot attribute, such as tape and shrink wrap.

---

## Q35 — A mis-scanned bag can never be corrected
**Source:** Part 5 §8, §19; Part 1 §5.3 · **Status:** open · **Impact:** medium

§8 states a bag, once assigned, cannot be assigned again, and §19 makes every assignment permanent. There is no un-assign, move, or correction operation anywhere in the module.

Two situations make this a problem in practice. A worker scans the wrong bag — now that bag is permanently attached to the wrong pallet and the database no longer matches the physical pallet. And bags are sometimes physically pulled off a pallet, which the system cannot represent at all.

Because scanning is mandatory (Part 1 §5.3), errors here are not avoidable by working around the system; they are locked in by it.

Suggested fix: keep assignments append-only, but allow an authorized user to record a *reversal* row (with reason, user, timestamp) rather than deleting. The bag returns to `Waiting For Pallet` and its history shows both events — consistent with the immutability philosophy and with Q17's approach to corrections.

---

## Q36 — Pallet capacity of 15 is fixed in prose, and partial pallets cannot close
**Source:** Part 5 §6, §5, §19 · **Status:** open · **Impact:** medium

Two related problems with capacity.

**It is hardcoded.** §6 says a pallet "normally contains 15 Produced Bags", but 15 appears nowhere as data. Meal boxes or a new plate size will not be 15, and Part 1 §9 requires future products without schema redesign. Capacity belongs on a master table — keyed by product type and plate size — not in code.

**Partial pallets cannot be closed.** §19 says a pallet cannot become `Ready` until it reaches the required capacity. At the end of a run or a shift there will be a pallet with fewer than 15 bags. As written it can never leave `Building`, so it can never be stored — and its bags stay in limbo. This is the same shape as Q21's partial-bag problem, one level up.

**Needs from you:** does a short pallet get held open until the next run tops it up, or closed short and stored as-is? If it is ever closed short, the module needs an explicit "close early" action with a reason.

---

## Q37 — BagPalletAssignments needs a unique constraint to enforce §8
**Source:** Part 5 §8, §9 · **Status:** open · **Impact:** low (implementation detail)

A junction table normally models many-to-many, but §8 and §19 make this one-to-many — a bag belongs to exactly one pallet. The table is still the right choice because it carries `AssignedByUserId` and `AssignedDate`, which a plain FK on `ProducedBags` could not.

The rule must be enforced in the database, not only in application code, or two tablets scanning the same bag concurrently will both succeed:

```sql
CREATE UNIQUE INDEX ux_bag_single_pallet
  ON "BagPalletAssignments" ("ProducedBagId");
```

If Q35 introduces reversal rows, this becomes a partial unique index filtered to active assignments.

---

## Q38 — Barcode symbology and format are never specified
**Source:** Part 5 §11–§13; Part 3 §7; Part 4 §20 · **Status:** largely answered by Part 8 §11–§12, §16 · **Impact:** high

> **Update (Part 8):** three of the four points are settled. **Uniqueness** is global across the ERP and barcodes are never reused (§16). **Encoded value** is an ERP-generated unique identifier, with the human-readable form printed alongside it on the label so a damaged label can still be keyed by hand (§12). **Symbology** is deliberately deferred to implementation, with the database kept independent of it (§11) — a sound decision, though the hardware question below still has to be answered before labels are designed. The remaining point, **type prefix**, is now **Q65**.

Five parts describe a barcode-first factory without ever specifying what a barcode *is*. Four decisions are missing, and all four affect hardware, printing and the scanning screens:

1. **Symbology** — Code 128 is the usual choice for alphanumeric factory labels; QR is better if tablet cameras do the scanning, since it reads at angles and survives damage.
2. **Encoded value** — the human-readable code (e.g. `13BABS240526A`) or an opaque ID? Encoding the readable code lets a worker key it manually when a label is damaged; an opaque ID is shorter and stable but useless once unscannable.
3. **Type prefix** — with rolls, bags and pallets all scanned into different screens, a scanned value must reveal *what kind of object it is*. A prefix (`R-`, `B-`, `P-`) lets a screen reject a wrong-type scan immediately instead of failing an unrelated lookup, and guarantees the three sequences can never collide.
4. **Uniqueness scope** — is uniqueness enforced per type or across all barcodes? A single global unique index is simpler and makes "scan anything, find it" possible.

**Needs from you:** are workers scanning with tablet cameras or dedicated handheld scanners, and what label printers exist? Part 1 §6 says Android tablets, which points toward QR — but this should be confirmed before any label is designed.

---

## Q39 — Packaging materials: per-pallet in the workflow, per-shift in the table
**Source:** Part 5 §2, §15, §16 · **Status:** open · **Impact:** low

The §2 workflow places "Consume Packaging Materials" immediately after "Pallet Becomes Full", implying consumption is recorded per pallet. §15 and §16 instead record shift totals entered at end of shift.

§15's reasoning is sound — the factory cannot measure partial tape or shrink per pallet — so the shift-level table is right and the workflow diagram is misleading. Worth correcting the diagram so it does not drive the UI toward a per-pallet prompt.

---

## Q40 — "Every action records User, Date, Time, Shift" has no mechanism
**Source:** Part 5 §14; Part 3 §9; Part 4 §5 · **Status:** largely answered by Part 8 §14 · **Impact:** medium

> **Update (Part 8):** an audit log is now specified — User, Date, Time, Action, Item, Notes, never deleted, with named actions including `Barcode Printed` and `Barcode Scanned`. That covers the mechanism this entry asked for. Two gaps remain and have moved to **Q63**: the audit record omits Shift although §8 requires it, and the log is scoped to *barcode* operations only, leaving recipe changes and inventory adjustments unaudited. `WoodenPallets` still lacks `ShiftReportId`.

§14 states every action records user, date, time and shift — the core of the worker-accountability objective in Part 1 §5.3. But this is implemented ad hoc: some tables have a user column, some have a shift, `WoodenPallets` has `CreatedByUserId` and `CreatedDate` but **no `ShiftReportId`**, and `BagPalletAssignments` has neither shift nor any record of a *failed* scan.

Two gaps matter. Actions that change nothing are invisible — a rejected incompatible-bag scan (§7) leaves no trace, though repeated rejects are exactly the behaviour a supervisor would want to see. And "who did what when" is spread across a dozen tables with no single place to query it.

Suggested: an `AuditLog` table (user, action, entity type, entity id, shift, timestamp, result) written by a single EF Core `SaveChanges` interceptor, plus `ShiftReportId` on `WoodenPallets` for consistency with every other production record.

---

## Q41 — Nothing records where a `Stored` pallet is
**Source:** Part 5 §5, §12, §20 · **Status:** deferred — likely the inventory part · **Impact:** low

Pallet status `Stored` implies a location, but no warehouse or location entity exists. §12 lists warehouse locations as a *future* barcode target and §20 defers location tracking, so this is a known v1 exclusion rather than an omission.

Noting only so the status is understood as "in the warehouse somewhere", not "at a known place" — and so the finished-goods inventory part is checked against it.

---

## Q42 — `Shift` is a bare value, not a master table
**Source:** Part 6 §9, §10; Part 2 · **Status:** open · **Impact:** medium

§9 makes the point that production tables should reference a *Shift Report* rather than a bare letter — but `ShiftReports` itself then stores `Shift` as an unqualified value with no type, no table and no definition.

Everywhere else the specification refuses to hardcode a list: colours, plate sizes, product types and templates are all master tables specifically so new values need no code change (Part 2 §5–§8). Shifts are the exception, and they carry more information than a letter — each has a nominal start and end time, which the system needs for two things it cannot currently do:

- **Assign a timestamp to a shift.** With no shift boundaries stored, nothing can determine which shift a given moment belongs to.
- **Handle the night shift crossing midnight.** A roll produced at 00:30 belongs to the shift that started the previous evening. Since `ProductionDate` and `Shift` are separate columns with no rule linking them to real time, that roll can plausibly be filed under either date.

Suggested: a `Shifts` master table (Name, StartTime, EndTime, IsActive) with `ShiftReports.ShiftId` as an FK. `ProductionStartTime` / `ProductionEndTime` on the report then read naturally as the *actual* times against the shift's *nominal* ones.

Also missing: a unique constraint on (`ProductionDate`, `ShiftId`), or the same shift can be opened twice on one day and production will split across two reports unnoticed.

**Needs from you:** the actual shift names and their clock times, and which calendar date a night shift is filed under — the day it starts or the day it ends.

---

## Q43 — Shift reports have no open/closed lifecycle
**Source:** Part 6 §9, §15, §18; Part 5 §16 · **Status:** open · **Impact:** high

`ShiftReports` has no status column, and nothing describes who creates a report or when. Yet the whole module depends on a lifecycle:

- Recycler production and packaging consumption are explicitly *end of shift* activities (§2, §15).
- §18 says a report cannot be deleted "once production data has been recorded" — a rule that implies a state change but does not define one.
- Every production record needs a `ShiftReportId` at the moment it is created, so a report must already exist before the first roll of the shift.

The unanswered questions are practical. Who opens the report — the supervisor at shift start, or does the first production event create it automatically? Can an operator still post a roll to yesterday's shift? Once the recycler figure is entered, is the shift closed, and what happens to a late correction?

Without a status, a shift is never finished, backdated entries are indistinguishable from real ones, and "did every shift get its recycler reading?" is unanswerable.

Suggested: `Status` (`Open` | `Closed`) with `ClosedByUserId` / `ClosedAt`; production may only post to an `Open` report; closing requires the recycler and packaging records to exist; reopening is an administrator action with a reason, recorded in the audit log (Q40).

**Needs from you:** does the supervisor open the shift in the ERP at the start, or should the system open one automatically on first use?

---

## Q44 — Machine settings are hardcoded and not attributed to a machine
**Source:** Part 6 §10, §12; Part 1 §3 · **Status:** open · **Impact:** medium

`MachineSpeed`, `FeedDistance` and `CycleTime` are three fixed columns on `ShiftReports`. Three problems, in increasing order of consequence:

1. **Which machine?** A shift report is factory-wide (§17), covering extruder, thermo and recycler lines, but there is one set of settings. Presumably these are thermo settings — the specification never says. If a second thermo machine is ever added, the columns become meaningless.
2. **Settings change during a shift.** One row per shift can only hold one value, so an adjustment mid-shift is either lost or overwrites the original. §12's stated purpose — correlating settings with quality and waste — is weakened if the setting recorded is not the one that was running when a given roll was made.
3. **Adding a fourth setting needs a migration.** This is the same anti-pattern as Q31, and the same fix applies: a `MachineSettingTypes` master table plus a `ShiftMachineSettings` child table (ShiftReportId, MachineId, SettingTypeId, Value, RecordedAt). New settings then become data.

The lighter version, if per-roll settings are overkill: keep them on the shift report but tie them to a machine and allow more than one row per shift.

**Needs from you:** are these thermo settings, and do they realistically change within a shift?

---

## Q45 — Shift Summary re-stores values the database already holds
**Source:** Part 6 §16, §11; Part 5 §10 · **Status:** open · **Impact:** medium

§16 lists six summary figures and says "some values may be entered manually, while others can be calculated automatically" — without saying which are which, and without specifying a table.

Every one of the six is already derivable:

| Summary value | Already available from |
|---|---|
| Total Roll Weight Used | `SUM(RollTestReports.Weight)` for rolls consumed in the shift |
| Total Scrap Weight | `RecyclerProductions.ScrapWeight` |
| Loss Percentage | computed from the same row |
| Produced Recycled Material | `RecyclerProductions.ProducedRecycleWeight` |
| Total Finished Bags | `COUNT(ProducedBags)` via thermo productions in the shift |
| Total Finished Pallets | `COUNT(WoodenPallets)` for the shift |

Storing them again would contradict the rule the specification states twice: §11 here ("the calculated value should not be permanently stored") and Part 5 §10 ("avoid storing calculated values whenever possible"). Three of the six would be verbatim copies of columns in the adjacent `RecyclerProductions` row.

Suggested: treat the Shift Summary as a **read-only report or database view**, not a table. If any figure genuinely comes from a physical measurement the system cannot derive — total roll weight might, if untested rolls exist (Q14) — then that one field is a real input and should be named as such.

**Needs from you:** is any of the six actually weighed or counted by hand, rather than added up from records?

---

## Q46 — Recycled material enters inventory as an anonymous pool
**Source:** Part 6 §3, §7; Part 2 §13 · **Status:** open · **Impact:** medium · *Extends Q5*

§7 increases the stock quantity of the single "Recycle" material. There is no lot, batch or shift identity on the inventory that results, so once two shifts' output is added the two are indistinguishable.

This makes the traceability boundary from Q5 sharper than first noted. It is not merely that a Black roll traces back to "a recycled lot from some shift" — it traces back to a **running balance with no shift attribution at all**. If a defect is suspected to originate in contaminated scrap, there is no way to identify which shifts' recycled material could be involved, and therefore no way to bound which rolls are affected.

Two options:
- **Accept the pool** (matches Part 6 §1's "follow the factory's real workflow") and state the limit plainly in traceability reports, so nobody believes the chain is complete.
- **Add lot identity** — each recycler production creates a numbered lot, and extruder withdrawals of Recycle name the lot. This costs the operator one extra selection and only helps if lots are kept physically separate, which they may well not be.

**Needs from you:** is recycled material stored in separate containers per shift, or tipped into one common bin? If it is one bin, lot tracking would be fiction and option one is the honest choice.

---

## Q47 — LossPercentage is stored although it is derivable
**Source:** Part 6 §5, §8, §11 · **Status:** open · **Impact:** low

`RecyclerProductions.LossPercentage` is a stored column, while §8 says the ERP "may calculate this automatically" and §11 states calculated values should not be stored. Once stored, it can disagree with `(ScrapWeight − ProducedRecycleWeight) / ScrapWeight`.

Either compute it on read, or use a PostgreSQL generated column so it cannot drift:

```sql
ALTER TABLE "RecyclerProductions"
  ADD COLUMN "LossPercentage" numeric
  GENERATED ALWAYS AS (
    CASE WHEN "ScrapWeight" > 0
      THEN ("ScrapWeight" - "ProducedRecycleWeight") / "ScrapWeight" * 100
    END
  ) STORED;
```

Also worth a validation rule: `ProducedRecycleWeight` should never exceed `ScrapWeight`, which would produce a negative loss.

---

## Q48 — §17's relationship list omits the extruder
**Source:** Part 6 §17; Part 3 §3, §9 · **Status:** open · **Impact:** low

The shift report is described as the parent of thermo productions, shift workers, packaging consumption, recycler production and inventory movements — but not **Rolls** or **material withdrawals**, both of which carry `ShiftReportId` per Part 3 §3 and §9.

Almost certainly an omission in the diagram rather than a design decision, but worth confirming, since it is the list a reader would use to check that every child relationship has been implemented.

---

## Q49 — `RoleDuringShift` is untyped
**Source:** Part 6 §13; Part 1 §7 · **Status:** open · **Impact:** low

`ShiftWorkers.RoleDuringShift` has no type or source. Part 1 §7 already defines six roles, and ASP.NET Identity will hold them as real records, so a free-text column here would drift from those names and break any grouping by role.

Two readings: it records *which of their roles* the person worked in that shift (a worker may be qualified for more than one), in which case it should reference the Identity role. Or it is a job title independent of system permissions, in which case it needs its own small master table. The first seems more likely given §13's purpose.

---

## Q50 — Electricity meters: rollover and per-shift attribution
**Source:** Part 6 §10, §11 · **Status:** open · **Impact:** low

`End Meter − Start Meter` goes wrong in two ordinary situations: a physical meter that rolls over past its maximum returns a negative figure, and a meter replacement resets the count entirely.

Neither needs solving now — a validation warning when the result is negative, plus a note field, is enough for v1. Recorded so it is not discovered in production.

There is also a scope question: one meter reading per factory-wide shift report gives total consumption but cannot attribute it to a line, so "energy per kg produced" is available for the whole factory only, not per machine. Fine for v1 if understood; §19's OEE plans would eventually want per-machine metering.

---

## Q51 — Serialized items duplicate state they already carry
**Source:** Part 7 §1, §4, §13–§15; Part 3 §8; Part 4 §19; Part 5 §5 · **Status:** open · **Impact:** high

The Inventory table is asked to hold quantities for four `ReferenceType`s, but they are two different kinds of thing:

- **Fungible** — Materials. 3,250 kg of GPPS is a genuine balance; there is no such thing as "GPPS #7".
- **Serialized** — Rolls, Produced Bags, Wooden Pallets. Each is a unique row with its own identity, barcode and **Status** column.

For a serialized item the inventory quantity can only ever be 1 or 0 — which is precisely what its status already says. Roll status runs `Produced → Ready For Thermo → In Thermo → Consumed`; §13 then asks inventory to increase on production and decrease on consumption. That is the same fact stored twice, in two tables, updated by two code paths. When they disagree — and eventually they will — neither is authoritative.

The dashboard figures in §2 are all derivable:

```sql
-- "Produced Rolls: 18"
SELECT COUNT(*) FROM "Rolls" WHERE "Status" = 'Ready For Thermo';

-- "Finished Pallets: 145"
SELECT COUNT(*) FROM "WoodenPallets" WHERE "Status" = 'Stored';
```

This is the rule the specification has already stated twice — Part 5 §10 ("avoid storing calculated values whenever possible") and Part 6 §11 — applied to the same situation.

There is a second cost. `ReferenceType`/`ReferenceId` is a polymorphic key, so PostgreSQL **cannot** enforce a foreign key on it and EF Core cannot map it as a navigation property. Every join becomes a manual filtered query, and nothing prevents a row pointing at an id that does not exist. Accepting that for one table is a real trade; accepting it for the whole inventory is expensive.

Suggested: `Inventory` covers **materials only** — replace the polymorphic pair with a plain `MaterialId` FK, giving referential integrity, clean EF navigation, and a natural unique constraint. Roll, bag and pallet stock become status-driven views over their own tables. §18's "Roll Inventory", "Produced Bag Inventory" and "Finished Pallet Inventory" reports are then queries, not a second copy of the data.

**Needs from you:** confirmation that a roll, a bag or a pallet is never counted in fractions or partial quantities. If they are strictly whole units with a status, this simplification holds.

---

## Q52 — §18 requires two reports the data model cannot produce
**Source:** Part 7 §18, §11; Part 3 §3 · **Status:** open · **Impact:** high · *Escalates Q11*

The report list asks for **Consumption By Recipe** and **Production Yield**. Neither is computable as the model stands.

Material withdrawals carry `ShiftReportId` and `UserId` but no roll and no recipe version (Part 3 §3, restated in Part 7 §11). So if a shift runs Normal White and then ABS Black, the GPPS drawn for each is indistinguishable — consumption can be grouped by shift, never by recipe. Yield has the same defect: output per roll is known from the test report, but input per roll is not, so kg-in ÷ kg-out cannot be computed at any level below a whole shift.

This changes the standing of Q11. Until now the shift-level withdrawal was a *traceability* limitation the factory might reasonably accept. Part 7 now specifies two management reports that depend on roll- or recipe-level attribution, so the same gap has become a functional shortfall against a stated requirement.

Three ways to close it, cheapest first:

1. **Add `RecipeVersionId` to the withdrawal** — the operator already selected a recipe to start the run, so the system can stamp it with no extra input. Makes Consumption By Recipe exact; yield stays shift-level.
2. **Add nullable `RollId`** — makes both reports exact, at the cost of the operator withdrawing per roll rather than per batch.
3. **Keep shift-level and drop the two reports** — honest, and fine if the factory does not actually need them.

**Needs from you:** the same question as Q11 — does the operator mix per roll or per batch — plus whether these two reports are ones management genuinely wants.

---

## Q53 — Movement sign is undefined
**Source:** Part 7 §6, §7 · **Status:** open · **Impact:** high (blocking for implementation)

`InventoryMovements.Quantity` has no stated sign convention, and `MovementType` does not reliably supply one. `Receive` is clearly positive and `Consumption` clearly negative, but **`Adjustment` goes both ways** — a stock count can find more or less than expected — and `Transfer` is directionless until locations exist (§19).

This must be settled before any code is written, because it determines whether the history can be summed at all:

- **Signed quantity** (+120 / −50): `SUM(Quantity)` reconstructs the balance directly, and the never-negative rule becomes a single check. Downside — a stray sign error silently corrupts a total.
- **Unsigned quantity plus an explicit `Direction` column** (`In`/`Out`): impossible to store an ambiguous row, and every report must remember to apply the direction.

The first is simpler and standard for ledger-style tables. Either way it needs stating explicitly, along with which movement types are valid in which direction.

---

## Q54 — A movement cannot name the transaction that caused it
**Source:** Part 7 §6, §16 · **Status:** open · **Impact:** medium

§16 says movements "reference Production Modules", but `InventoryMovements` has no column that does so. `ReferenceType`/`ReferenceId` identify **what item** moved, not **what event** moved it.

So from a movement of −120 kg GPPS you can reach the material, the shift and the user, but not the thermo production, recycler production or packaging consumption record that caused it. The reverse direction fails too: from a recycler production you cannot find the movement it generated, which makes it impossible to verify that every recycler report actually posted its stock increase, or to reverse one cleanly if a report is corrected.

Suggested: a second pair — `SourceDocumentType` / `SourceDocumentId` — or, better given the polymorphism problem in Q51, explicit nullable FKs for the handful of real sources (`RecyclerProductionId`, `PackagingConsumptionId`, `ThermoProductionId`, `RollId`). There are only a few, and real FKs stay checkable.

This also matters for corrections: with a link to the source, reversing a mistaken recycler entry is a matter of finding its movements and posting compensating rows.

---

## Q55 — Bags and the pallet holding them are both counted
**Source:** Part 7 §14, §15; Part 5 §3 · **Status:** open · **Impact:** medium

§14 states that a bag assigned to a pallet still exists and merely changes status, so it stays in Produced Bag inventory. §15 then adds the finished pallet to inventory once it is `Ready`.

The same 15 bags are now counted twice — once individually and once as the pallet containing them. Any "total finished goods" figure that adds the two will be roughly double the truth, and the discrepancy grows as more pallets are built.

Three consistent options: count bags only (pallets are packaging); count pallets only, with loose bags counted until assigned; or count both but never sum them, with the reports labelled so nobody does. The second matches how the factory actually ships and is the usual choice.

Note this becomes much simpler under Q51 — if bag and pallet stock are status-driven views, "unassigned bags" and "stored pallets" are naturally disjoint queries and the double count cannot arise.

---

## Q56 — CurrentQuantity is itself a stored calculated value
**Source:** Part 7 §2, §3; Part 5 §10; Part 6 §11 · **Status:** open · **Impact:** medium

`Inventory.CurrentQuantity` is exactly `SUM(InventoryMovements.Quantity)` for that item. The specification elsewhere rules stored calculations out twice (Part 5 §10, Part 6 §11), and Part 7 §10 of that same rule-set would suggest deriving it.

Here the exception is justified — summing a movement ledger on every screen refresh gets slow as history grows, and stock levels are read constantly. But it should be recorded as a **deliberate** exception rather than an oversight, with two consequences accepted:

- Balance and ledger can drift if any code path writes one without the other. Every update must occur in a single transaction that writes both rows, never one alone.
- A reconciliation routine is needed — recompute every balance from movements and report differences. Run it nightly or on demand; it is the only way to detect drift, and it is also what proves the system is sound after a crash.

---

## Q57 — The never-negative rule needs enforcement and an escape valve
**Source:** Part 7 §17, §9; Part 3 §3 · **Status:** open · **Impact:** medium · *Supersedes Q19*

§17 requires quantities never to go negative. Two things are missing.

**Enforcement.** Application-level checking is not sufficient — two tablets withdrawing the same material at once can both read 100 kg, both pass a check for 60 kg, and both write. The balance row must be locked for update within the transaction, backed by a database constraint as a final guard:

```sql
ALTER TABLE "Inventory" ADD CONSTRAINT ck_inventory_non_negative
  CHECK ("CurrentQuantity" >= 0);
```

**An escape valve.** If the ERP balance reads 0 while material physically exists — the ordinary result of a miscount or an unrecorded delivery — a hard block stops the extruder until an administrator is found. Given that Part 1 §5.3 makes ERP use mandatory, the realistic outcome is that production continues unrecorded, which is the exact failure the system exists to prevent.

Suggested: keep the hard block, but make the adjustment path (§9) fast and available on the tablet to a supervisor, not only to a desktop administrator. The block is then a prompt to correct the record rather than a reason to bypass it.

---

## Q58 — InventoryMovements repeats ReferenceType/ReferenceId
**Source:** Part 7 §4, §6 · **Status:** open · **Impact:** low

A movement carries `InventoryId`, and the `Inventory` row it points to already holds `ReferenceType` and `ReferenceId`. Repeating both on the movement allows a row whose reference disagrees with its own parent.

Either drop them from the movement and reach them through `InventoryId`, or keep them as a deliberate immutable snapshot and never write them independently. The first is cleaner; the second only earns its place if movements must survive the deletion of an inventory row, which §17 does not contemplate.

---

## Q59 — "Wooden Pallet inventory" means two different things
**Source:** Part 7 §8, §15; Part 5 §16; Part 1 §5.2 · **Status:** open · **Impact:** medium · *Related to Q34*

Two distinct items share one name. **Empty Wooden Pallets** are a packaging *material*, bought in and consumed (Part 1 §5.2, Part 5 §16). **Finished Wooden Pallets** are a produced *good*, created when bags are assigned (Part 7 §15).

§8's row "Finished pallet stored → Wooden Pallet inventory increases" reads as though the packaging material went up, when in fact one empty pallet was consumed and one finished pallet was created. The two move in opposite directions at the same moment.

They need distinct names in the schema and on every screen — `EmptyWoodenPallet` (Material) and `FinishedPallet` (produced unit). Q34 is the same confusion seen from the packaging side: the empty pallet should be deducted automatically when a pallet record is created, not typed in again as a shift total.

---

## Q60 — Inventory.UnitId duplicates Materials.UnitId
**Source:** Part 7 §4; Part 2 §4 · **Status:** open · **Impact:** low

`Materials` already defines each material's unit. Repeating `UnitId` on `Inventory` allows a material defined in kg to hold a balance labelled in pieces.

Drop it and read the unit through the material (see also Q18, the same duplication on withdrawals). If Q51 is adopted and inventory becomes material-only, this resolves itself — the unit comes from the `MaterialId` FK.

---

## Q61 — The last step of the flagship traceability chain is not implementable
**Source:** Part 8 §9, §17; Part 3 §3; Part 7 §18 · **Status:** open · **Impact:** high · *Third escalation of Q11*

§9 walks through the ERP's headline demonstration — scan a pallet, drill to a bag, to the thermo production, to the roll, to the recipe version, to the ingredients, and finally to **"Display raw materials consumed"**. §17 repeats the chain with `Raw Materials → Recipe Version → Roll` at its head.

Every arrow in that chain is a real foreign key except the last one. Withdrawals record material, quantity, user, date and shift — never a roll and never a recipe version (Part 3 §3, Part 7 §11). So the screen can display the recipe's *intended* ingredient percentages, but not the materials actually consumed for that roll. For a shift that ran two recipes, it cannot even narrow them.

This is the same gap as Q11 and Q52, now blocking the feature the whole barcode module exists to deliver. Three parts have specified something that depends on it: Part 7 §18's two reports, Part 8 §9's drill-down, and Part 1 §5.4's traceability objective.

Worth being precise about what is achievable, since the distinction decides how the screen is labelled:

| Level of attribution | Cost to the operator | Answers §9's last step? |
|---|---|---|
| Shift only (as specified) | none | No — only "materials withdrawn during this shift" |
| Recipe version stamped on withdrawal | none — already selected | Partly — actual materials per recipe run |
| Roll id on withdrawal | withdraws per roll, not per batch | Yes |

**Needs from you:** the answer to Q11 — per roll or per batch. If per batch, §9's final step should be reworded to "materials withdrawn during this shift" so the system does not promise more than it can show.

---

## Q62 — Two incompatible human-readable formats for the same roll
**Source:** Part 8 §12; Part 3 §6 · **Status:** open · **Impact:** medium

Part 3 §6 defines the Roll Code as `13BABS240526A`, encoding production number, colour, family, date and shift. Part 8 §12 shows a label reading `ROLL-20260815-00125`. These are different schemes for the same physical object.

A roll would then carry three identifiers: `Id`, `RollCode`, and whatever the label prints — with no rule saying which a worker quotes when reporting a problem, or which the manual-entry fallback in §12 expects.

The two serve different purposes and only one should survive on the label. The Part 3 code is information-dense and lets a supervisor read colour, recipe family and shift off a roll without a scanner — genuinely useful on the floor. The Part 8 format is sortable and collision-proof but tells a human nothing they could not get by scanning.

Suggested: keep `RollCode` from Part 3 as *the* human-readable identifier, print it on the label above the barcode, and let the barcode encode the opaque id. Then there are two identifiers with distinct jobs, not three.

**Needs from you:** does the factory already use the `13BABS240526A` convention on paper today? If so it should win on familiarity alone.

---

## Q63 — Audit log omits shift and covers only barcode operations
**Source:** Part 8 §8, §14; Part 5 §14 · **Status:** open · **Impact:** medium

Two gaps in an otherwise well-specified audit design.

**Shift is missing.** §8 states every scan is associated with user, date, time and shift, but the §14 record is User, Date, Time, Action, Item, Notes. Without `ShiftReportId` the log cannot answer "what did shift B do on the 14th" without inferring the shift from a timestamp — which is not possible anyway while shift boundaries are undefined (Q42).

**Scope is too narrow.** §14 says "every *barcode-related* operation". The operations most needing an audit trail are not barcode operations at all: creating a recipe version, an administrator's inventory adjustment (Part 7 §9), an admin correction to a roll measurement (Q17), reopening a closed shift (Q43). These are the high-privilege actions, and as written none of them is logged.

Suggested: widen it to an application-wide audit log written by a single EF Core `SaveChanges` interceptor, with `ShiftReportId` included. Barcode scans become one action type among several rather than the only one.

Also worth capturing: **failed** operations. A rejected incompatible-bag scan (Part 5 §7) changes no data and so leaves no trace, yet a worker repeatedly scanning wrong bags is exactly what a supervisor would want to see.

> **Update (Part 10 §14):** the scope half of this is resolved — the action list now includes `Inventory Adjustment`, `Recipe Created` and `Recipe Version Created`, so the log is application-wide rather than barcode-only. Shift is still absent from the recorded fields, and the table itself is still missing from the Part 9 schema (Q70).

---

## Q64 — No rule for reprinting a label or for manual entry
**Source:** Part 8 §11, §12, §13, §16 · **Status:** open · **Impact:** medium

Labels on styrofoam in a factory get torn, soaked and rubbed off. Two everyday consequences are unaddressed.

**Reprinting.** §16 says barcodes are never reused, which is about not issuing a retired *value* to a new item — but it reads as though reprinting a damaged label is forbidden. It must be allowed, or a roll with a ruined label can never be scanned again. The rule should be explicit: reprinting reissues the *same* barcode for the *same* item, and each reprint is logged (`Barcode Printed` already exists as an action in §14), so a supervisor can see if one operator reprints unusually often.

**Manual entry.** §12 puts a human-readable identifier on the label precisely so workers can identify items "even if the scanner is unavailable", and §13 says not to type identifiers "if scanning is possible" — both implying a typing fallback. But Part 1 §5.3 and Part 8 §1 want scanning mandatory so the ERP cannot be bypassed.

These reconcile cleanly if manual entry is permitted but *marked*: the audit record distinguishes `Scanned` from `Manually Entered`, and a report shows manual-entry rates by user. Work never stops, and the accountability goal is served better than by a prohibition workers would have to break.

**Needs from you:** who is allowed to reprint — any operator, or a supervisor only?

---

## Q65 — A scanned value cannot tell the system what it is
**Source:** Part 8 §11, §16; Part 5 §11 · **Status:** open · **Impact:** medium · *Remainder of Q38*

§11 says the barcode "may simply contain a unique identifier generated by the ERP", and §16 requires that scanning always retrieves the associated object. With three types of object scanned into three different screens, the value alone must reveal which kind it is.

Without a type marker, a bag barcode scanned into the pallet field produces a failed lookup rather than "that is a bag, not a pallet" — and every scan needs three queries to work out what it hit.

Two clean options: a type prefix in the encoded value (`R-…`, `B-…`, `P-…`), which makes the wrong-type case an instant, precise error; or a single `Barcodes` table holding every issued value with its object type, which enforces §16's global uniqueness in one place and gives the universal lookup endpoint a single index to hit.

The second is stronger given the requirement that barcodes are unique across the ERP and never reused — one table can guarantee both, where three independent sequences cannot.

---

## Q66 — What happens when the network drops
**Source:** Part 8 §1, §13; Part 1 §6 · **Status:** open · **Impact:** medium

Part 1 §6 puts every user on Android tablets against a Windows Server over the local network, and Part 8 makes scanning the mandatory entry point for thermo, packaging and warehouse work.

If Wi-Fi drops mid-shift, production either stops or continues unrecorded — and unrecorded production is the failure the ERP exists to prevent. Since scanning is compulsory, there is no legitimate fallback for the operator.

For v1 the cheap mitigations are worth deciding now rather than after the first outage: keep the scanning screens usable with a clear "not connected" state and a retry, rather than silently failing; and make sure a submitted scan is either committed or visibly rejected, never lost. Full offline queueing is a much larger piece of work — it needs client-side storage, conflict handling and duplicate suppression — and is best treated as a v2 item, but the decision should be conscious.

**Needs from you:** how reliable is factory Wi-Fi in practice, particularly around the extruder and thermo machines?

---

## Q67 — "Inventory Status" on the pallet is a second status
**Source:** Part 8 §6; Part 5 §5; Part 7 §15 · **Status:** open · **Impact:** low · *Supports Q51*

§6 lists what a pallet barcode identifies, including both **Status** and **Inventory Status**. Part 5 §5 defines one status (`Building`/`Ready`/`Stored`/`Shipped`), and Part 7 §15 has the pallet separately present in the Inventory table once `Ready`.

So the pallet has a lifecycle status and an inventory presence expressing the same fact, which is exactly the duplication described in Q51. Under the material-only inventory proposed there, "inventory status" stops being a separate concept — a pallet is in finished goods precisely when its status is `Stored`, and one column answers both.

---

## Q68 — Roll barcode claims a product type the roll does not have
**Source:** Part 8 §4; Part 3 §9; Part 2 §5 · **Status:** open · **Impact:** low · *Instance of Q15/Q24*

§4 lists Product Type among the information a roll barcode retrieves, but `Rolls` (Part 3 §9) has no `ProductTypeId`. It is reachable only through `RecipeVersion → RecipeFamily → ProductTypeId`, which works — but it means the product type of a roll is decided by its *recipe*, not by what is actually made from it, and the mold that determines the real product is never recorded (Q24).

Resolving Q24 fixes this: with the template on the thermo production, a roll's product type is a plan and the bag's is a fact.

---

## Q69 — Six relationships are declared 1:1 but none of them can be
**Source:** Part 9 §6; Parts 3–6 · **Status:** open · **Impact:** high (blocking for implementation)

§6 is the authoritative cardinality list, and six of its rows say `1 → 1` where the real relationship is `1 → 0..1`:

| Declared | Reality | Why |
|---|---|---|
| Roll → RollTestReport | 0..1 | The report is written after production; the roll exists first, and may never be tested (Q14, Q16) |
| Roll → ThermoProduction | 0..1 | Failed rolls are donated or sampled and never reach thermo (Q25) |
| ThermoProduction → ThermoTestReport | 0..1 | QC writes it after the run, possibly on a later shift (Q22) |
| ProducedBag → BagPalletAssignment | 0..1 | Part 5 §19 gives bags a `Waiting For Pallet` status — that state *is* an unassigned bag |
| ShiftReport → RecyclerProduction | 0..1 | Written at end of shift; absent all shift, and absent entirely if the recycler did not run |
| ShiftReport → PackagingMaterialConsumption | 0..1 | Same — an end-of-shift entry |

This is not a documentation nicety. In EF Core a **required** one-to-one dependent cannot be saved without its principal *and the principal cannot be saved without it*, so modelling these literally means a roll cannot be created until its test report exists, and a shift report cannot be opened until its recycler figures are known. Both are impossible in the real workflow — production always precedes the paperwork.

The correct implementation in every case is a nullable, unique foreign key on the dependent:

```sql
-- one test report per roll, but a roll may have none
CREATE UNIQUE INDEX ux_rolltestreport_roll ON "RollTestReports" ("RollId");
```

with `.HasOne(...).WithOne(...).IsRequired(false)` in EF Core. The uniqueness enforces "at most one"; the nullability allows "not yet".

Worth stating explicitly in the spec, because "1 → 1" read literally by whoever builds the model produces a schema that cannot record a normal day's work.

---

## Q70 — The audit log is missing from the table list
**Source:** Part 9 §4, §15; Part 8 §14 · **Status:** open · **Impact:** medium

Part 8 §14 specifies an audit log — User, Date, Time, Action, Item, Notes, never deleted — and Part 5 §14 makes it central to the worker-accountability objective. It appears in neither §4, §15 nor §16 of Part 9.

Since §15 is presented as the complete table summary, the omission would carry straight into the schema. It is also the one table whose absence is invisible at runtime: nothing fails, the log simply is not there, and the accountability requirement quietly goes unmet.

Related: `Shifts` (Q42) and any withdrawal table (Q72) are likewise absent, and `Templates` is listed but referenced by nothing (Q24).

---

## Q71 — `UpdatedAt` / `UpdatedBy` contradict the immutability rule
**Source:** Part 9 §13, §2; Part 1 §9; Part 3 §15 · **Status:** open · **Impact:** medium

§13 recommends `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` on most transactional tables. §2 principle 2 — one page earlier — says production history is never deleted and recipe versions are never edited, and Part 1 §9 makes immutable production records a founding principle.

`UpdatedBy` columns exist to record in-place edits. Putting them on production tables signals that rows are expected to be overwritten, and an overwrite destroys the original value — the exact outcome Q17 flagged for admin corrections to roll measurements.

The two can be reconciled by scope: `CreatedAt`/`CreatedBy` on production tables (they are written once), the full four on master data (which legitimately changes), and corrections to production records handled by a revision table that keeps old and new values rather than by an in-place update.

Otherwise the schema advertises mutability the philosophy forbids, and whoever implements it will reasonably follow the schema.

---

## Q72 — The complete table list confirms there is no withdrawal record
**Source:** Part 9 §4, §15, §16; Part 3 §3; Part 7 §11 · **Status:** open · **Impact:** high · *Confirms Q11, Q52, Q61*

Until now the absence of a material-withdrawal table could have been an omission in the narrative. Part 9 presents the complete schema, and there is no such table in §4 or §15 — so a withdrawal is an `InventoryMovement` and nothing more.

That settles the question definitively. `InventoryMovements` carries `ShiftReportId`, `UserId`, `MovementType` and the item reference, but no roll, no recipe version and no link to the production event that caused it (Q54). §16's claim that "InventoryMovements connect inventory with every production event" is therefore not supported by the columns.

The consequence, stated plainly: **as designed, the system cannot report what any individual roll consumed, and cannot attribute consumption to a recipe.** Four separate requirements depend on it — Part 1 §5.4, Part 7 §18's two reports, Part 8 §9's drill-down and §17's chain.

The cheapest repair remains two nullable columns on `InventoryMovements` — `RecipeVersionId` and, if per-roll mixing is real, `RollId`. Both are additive; neither disturbs anything else in the design.

---

## Q73 — Soft delete plus restrict-delete needs a query-filter decision
**Source:** Part 9 §10, §14 · **Status:** open · **Impact:** medium

§10 and §14 together are the right approach — nothing is ever deleted, unused master data is flagged `IsActive = false`. But the EF Core mechanism for it has a trap worth naming before it is discovered in testing.

The usual implementation is a global query filter (`HasQueryFilter(m => m.IsActive)`), which silently excludes inactive rows from *every* query — including joins from historical production records. A roll produced in 2026 with a colour later deactivated would load with a null `Color` navigation, and a traceability screen would show a blank field for a record that is perfectly valid. EF Core will also warn about required navigations to filtered entities.

Two consistent choices: apply query filters only to the master-data *pickers* (via a dedicated query rather than a global filter), keeping historical joins unfiltered; or use global filters and mark every historical navigation `IgnoreQueryFilters()`. The first is less error-prone, because the failure mode of the second is silently missing data rather than an exception.

Also unaddressed: what happens when a material is deactivated while it is still an ingredient in a `Current` recipe version. Deactivation should either be blocked or warn, or the next production run will reference an inactive material.

---

## Q74 — Timestamp naming is inconsistent across the schema
**Source:** Part 9 §11, §13; Parts 3–7 · **Status:** open · **Impact:** low

§11 recommends indexing `ProducedDate`, but no such column exists anywhere. The equivalents across the spec are `ProducedAt` (Rolls, ProducedBags), `MovementDate` (InventoryMovements), `CreatedDate` (WoodenPallets), `RecordedAt` (RecyclerProductions), `AssignedDate` (BagPalletAssignments), `ProductionDate` (ShiftReports), `StartedAt`/`FinishedAt` (ThermoProductions).

Four conventions for one concept. §13 then adds `CreatedAt`/`UpdatedAt`, making a fifth. Worth settling on one suffix — `…At` for instants, reserving `…Date` for genuine calendar dates such as `ShiftReports.ProductionDate` — before the first migration, since renaming columns afterwards is far more disruptive.

While consolidating, the unique constraints implied across the spec are also worth listing in one place, as several are load-bearing and easy to miss:

| Constraint | Source |
|---|---|
| One `Current` version per recipe family (partial index) | Q6 |
| One assignment per produced bag | Q37 |
| One test report per roll / per thermo production | Q69 |
| One recycler + one packaging record per shift report | Q69 |
| One shift report per (date, shift) | Q42 |
| Barcode unique across all three tables | Part 8 §16, Q65 |
| One inventory row per material | Q51 |

---

## Q75 — Principle 1 is contradicted in six places
**Source:** Part 9 §2 · **Status:** open · **Impact:** medium (summary entry)

§2's first principle — "information should only be stored once whenever possible" — is stated well and illustrated correctly with roll weight. Collected in one place, here is where the schema currently departs from it:

| Duplicate | Also derivable from | Entry |
|---|---|---|
| `ProducedBags.ColorId`, `.ProductTypeId` | Bag → Production → Roll | Q26 |
| `Inventory.UnitId` | `Materials.UnitId` | Q60 |
| `RecyclerProductions.LossPercentage` | scrap and produced weights | Q47 |
| `Inventory.CurrentQuantity` | `SUM(InventoryMovements.Quantity)` | Q56 |
| Shift Summary figures | production records | Q45 |
| Roll/bag/pallet inventory rows | their own `Status` columns | Q51 |
| `InventoryMovements.ReferenceType/Id` | parent `Inventory` row | Q58 |

Two of these are defensible as deliberate exceptions — `CurrentQuantity` for read performance, and the bag's colour/product type as an immutable snapshot. The rest look unintended. The distinction matters: a documented exception gets a reconciliation routine and a rule about who may write it, while an accidental duplicate just drifts.

---

## Q76 — Multiple warehouses would change the inventory key
**Source:** Part 9 §17; Part 7 §19 · **Status:** open · **Impact:** low

§17 states that none of the listed future modules require redesigning the database. That holds for most of them — Suppliers, Customers, Purchase Orders and Sales Orders are new tables with new foreign keys, and Machine Integration is additive.

**Multiple warehouses is the exception.** Today there is one inventory row per item; with warehouses there is one per item *per location*, which changes the table's natural key, the uniqueness constraint, and every balance query and stock check written against it. That is a migration with data implications, not a pure addition.

Not a problem for v1 — one factory, one store. Worth recording so the claim is understood as "mostly additive" rather than relied on later. If a second store is genuinely foreseeable, a nullable `LocationId` in the key now costs almost nothing.

---

## Q77 — An entire module has no role authorized to use it
**Source:** Part 10 §4; Part 5 §2, §7, §14; Part 8 §7 · **Status:** open · **Impact:** high (blocking)

The six roles cover extruder, thermo and recycler. **Packaging and warehouse are missing.**

Part 5 assigns real work to a "Packaging Worker" — creating pallets, scanning bags, assigning them, recording packaging material consumption — and to a "Warehouse Worker" who scans pallets and stores finished goods. Part 8 §7 puts both in the barcode workflow. Neither exists in §4.

Under §11's model, endpoints carry `[Authorize(Roles = "…")]`, so an operation with no role behind it is an operation nobody can perform. The whole of Part 5, plus the `Stored` transition in Part 7 §15, currently has no authorized user. The alternatives — leaving those endpoints unauthorized, or handing everyone the Administrator role — would each dissolve the accountability the module exists to provide.

Also unassigned: **receiving raw materials** (Part 7 §10) is listed under the administrator, which means a delivery cannot be booked in unless an administrator is present.

**Needs from you:** are packaging and warehouse separate jobs in the factory, or does one person do both? Either answer is fine — it just decides whether that is one new role or two.

---

## Q78 — "Supervisor" is referenced repeatedly but is not a role
**Source:** Part 10 §4, §5; Part 6 §10; Part 2 §9; Part 3 §4 · **Status:** open · **Impact:** high

A supervisor appears throughout the specification as someone with authority above an operator:

- `ShiftReports.SupervisorId` is a foreign key (Part 6 §10) — so a supervisor is already a stored fact.
- Part 2 §9: "The supervisor continuously adjusts ingredient percentages."
- Part 3 §4: "Recipe changes are only performed by administrators **or authorized supervisors**."

But §4 lists six roles and Supervisor is not among them, and §5 states the administrator "should be the **only** role allowed to create, edit or deactivate master data" — which directly contradicts Part 3 §4, since recipes are master data.

The practical consequence is that everything needing more authority than an operator but less than full system access falls to the administrator, who Part 1 §6 says works at a desktop computer. That includes recipe versions, inventory adjustments when a balance blocks production (Q57), reopening a closed shift (Q43), and correcting a mis-scanned bag (Q35) — all of which arise on the floor, on a tablet, at 2am.

Suggested: add a **Supervisor** role sitting between operator and administrator, holding recipe authoring, inventory adjustment, shift open/close and correction rights, while system configuration, user management and backups stay with the administrator.

**Needs from you:** does the factory have shift supervisors as distinct people from the administrator? The `SupervisorId` column suggests yes.

---

## Q79 — Shared tablets undermine the accountability objective
**Source:** Part 10 §1, §14; Part 1 §5.3, §6; Part 8 §8 · **Status:** open · **Impact:** high

Accountability is the stated purpose of the security model — §1 says every important action must be linked to the user who performed it, and Part 1 §5.3 makes worker accountability a primary business goal.

But Part 1 §6 puts users on tablets, which in a factory belong to *stations*, not people. The realistic pattern is that the first person to arrive logs in and the tablet stays logged in all shift. Every roll, scan and assignment for the next twelve hours is then attributed to that one account regardless of who actually pressed the button — and the audit trail becomes precise and wrong, which is worse than being absent, because reports would be trusted.

This is a workflow problem rather than a technical one, and the usual factory answers are cheap:

- **Short PIN re-entry per action** for the operations that matter (creating a roll, assigning a bag), with a full login only at shift start. Fast enough for a gloved hand, and ties each record to a person.
- **Badge or barcode login** — the worker's own barcode scanned into the same scanner already in their hand. Fits the barcode-first philosophy exactly and costs nothing extra in hardware.
- **Short idle timeout** returning to a lock screen. Simplest, but adds friction to every task and workers tend to defeat it.

Note that §19 defers "session timeout configuration" to a future version, which leaves v1 with no mechanism at all.

**Needs from you:** is a tablet assigned to a person or to a machine? If to a machine, one of the above is needed for the audit trail to mean anything.

---

## Q80 — JWT lifetime, revocation and shift length
**Source:** Part 10 §2, §12, §19 · **Status:** open · **Impact:** high

§2 specifies JWT tokens but no lifetime, no refresh mechanism and no revocation. Three consequences, all of which surface in the first week:

**Expiry versus shift length.** A twelve-hour shift outlasts any sensible token. If the token expires mid-run the operator is logged out mid-production; if it is issued for twelve hours to avoid that, a stolen or left-open session stays valid all day. The usual answer is a short access token (15–60 minutes) plus a refresh token, renewed silently — but §19 defers session timeout configuration to a future version, so v1 as written has neither.

**Deactivation does not take effect.** §12 lets an administrator deactivate a user, but a JWT is self-contained: the holder keeps full access until the token expires. Dismissing a worker mid-shift does not lock them out. This needs either short-lived tokens, or an `IsActive` check on each request.

**Token storage on a shared tablet.** A token in `localStorage` survives browser restarts and is readable by any script on the page — on a device several people use.

None of this is exotic; it is the standard JWT checklist. It just has to be decided before the login endpoint is written, because retrofitting refresh tokens touches every client call.

---

## Q81 — Backups live on the machine they protect
**Source:** Part 10 §17, §18 · **Status:** open · **Impact:** high

§17 is right about the important part — separate files, never auto-overwritten, multiple retention tiers. But §18's recovery procedure begins "install PostgreSQL, restore the latest backup" without saying where that backup is.

If backups are written to the same Windows Server, then the single most likely disaster — a failed disk — destroys the database and every backup together, and the recovery procedure cannot start. A backup stored only on the machine it protects is not a backup.

Three things to settle, none of them expensive:

- **Off-machine copy.** A second location — a NAS, another PC, an external drive rotated weekly, or cloud storage if the factory ever gets a connection. This matters more than backup frequency.
- **Retention.** "Never overwrite automatically" plus daily backups fills the disk, and a full disk stops PostgreSQL from writing. Keep e.g. 14 daily, 8 weekly, 12 monthly, and prune beyond that deliberately.
- **Restore testing.** A backup is only proven by a restore. Restoring to a scratch database quarterly is the only way to know the procedure in §18 actually works.

There is also an unstated **recovery point**. Daily backups mean up to 24 hours of production can be lost — for a system whose purpose is a permanent record of every roll, that is a whole shift's traceability gone. PostgreSQL WAL archiving gives point-in-time recovery and is the standard fix; worth deciding whether a day's loss is acceptable.

**Needs from you:** what other machine or storage exists on the factory network that backups could be copied to?

> **Update (Part 11 §16):** the most important point is answered — backups go to the server, an external hard drive and optionally cloud storage. That removes the single-disk failure mode. Still open: **retention** (never overwriting plus daily backups grows without bound), **restore testing**, and the **recovery point** — daily backups still mean up to a shift's traceability can be lost, which PostgreSQL WAL archiving would fix.

---

## Q82 — No transport security is specified
**Source:** Part 10 §2; Part 1 §6 · **Status:** open · **Impact:** high

Login credentials and JWT tokens cross the network on every request, and Part 1 §6 puts every user on Wi-Fi tablets. Nothing in the specification requires HTTPS.

Over plain HTTP on factory Wi-Fi, passwords and tokens are readable by anyone associated with the network, and a captured token is a working login for its whole lifetime. "It is only the local network" is the usual reason this gets skipped, and it is exactly the case where it is easiest to exploit.

This is not a large task: a self-signed certificate or an internal CA certificate installed on the tablets, HTTPS enforced with `UseHttpsRedirection` and HSTS. Worth doing before the first tablet is configured, since re-provisioning certificates on deployed devices is more work than starting with them.

---

## Q83 — Role names are inconsistent, and they are magic strings
**Source:** Part 10 §4, §11 · **Status:** open · **Impact:** low

§4 names the role **Administrator**; §11 authorizes on **"Admin"**. `[Authorize]` matches role strings exactly, so if the seeded role name and the attribute disagree the endpoint silently denies everyone — with no error, no log entry and nothing to grep for. The same applies to `ThermoOperator` versus "Thermo Operator".

Two conventions to fix it: define the canonical names once as constants and reference those in every attribute, and seed the roles from that same source so the database can never disagree with the code.

```csharp
public static class Roles
{
    public const string Administrator   = "Administrator";
    public const string ExtruderOperator = "ExtruderOperator";
    // …
}

[Authorize(Roles = Roles.Administrator)]
```

Worth settling now: role names appear in the database, in tokens and in every controller, and renaming them later means a data migration as well as a code change.

---

## Q84 — First administrator and password-reset lockout
**Source:** Part 10 §12, §13 · **Status:** open · **Impact:** low

§12 is right that there is no public registration — but then no user can exist until one is created by an administrator who does not yet exist. The first account has to be seeded on startup, with a forced password change on first login so the seeded credential cannot survive into production.

The mirror case: §12 gives password resets to administrators only and §13 makes passwords unrecoverable. If the sole administrator forgets theirs, nobody can reset it and nobody can create users. Either two administrator accounts exist as a matter of policy, or a documented break-glass procedure is needed at the server (a console command run locally, not an exposed endpoint).

---

## Q85 — Deactivated master data and running production
**Source:** Part 10 §16; Part 9 §14; Part 2 §11 · **Status:** open · **Impact:** low

§16 is consistent with Part 9 §14 — deactivate rather than delete, and history stays valid. Two edges are undefined:

- **Deactivating a material that a `Current` recipe version still uses.** The next production run would reference an inactive material. Deactivation should be blocked while the material appears in a non-archived version, or at minimum warn and name the versions affected.
- **Users.** §12 allows deactivating users, but Part 9 §14 lists `IsActive` only on Materials, RecipeFamilies, Templates and Colors. Identity provides `LockoutEnd` for this; worth stating which mechanism is used, since a deactivated user must still resolve as a name on every historical record they created.

See also Q73 for the EF Core query-filter trap that makes these historical references fail quietly rather than loudly.

---

## Q86 — Database migrations are never addressed
**Source:** Part 11 §7, §10, §11; Part 9 · **Status:** open · **Impact:** high (blocking)

Eleven parts describe the schema and the deployment pipeline, and neither mentions how schema changes reach the production database. The §10 workflow goes build → publish → restart, with no migration step. Yet every feature added after go-live will change tables that already hold production history that must never be lost.

Three decisions are needed before the first deployment:

**Who applies migrations.** The options are `Database.Migrate()` on application startup (simple, but two app instances racing at startup can both try, and a failed migration takes the app down with it), a `dotnet ef database update` step in the workflow (explicit, fails before the app restarts), or generating idempotent SQL scripts for review and manual application (safest, and appropriate for a database whose contents cannot be recreated). For a single-server factory ERP, the workflow step is the reasonable middle choice.

**What happens when a migration fails halfway.** PostgreSQL runs DDL transactionally, so a failed migration rolls back cleanly — but only if the whole migration is one transaction. EF Core's default is one transaction per migration, which is what you want; it is worth not overriding.

**Backup before migrate.** The deployment must take a backup immediately before applying any migration, not merely on the daily schedule. This is the difference between a bad migration costing minutes and costing a day of production records.

Also unaddressed: **seed data**. Units, material categories, colours, plate sizes, product types, the six roles and the first administrator (Q84) all need to exist on a fresh database. That belongs in a versioned seeding routine, not in a manual SQL script someone runs once and forgets.

---

## Q87 — There is no rollback path
**Source:** Part 11 §9, §10, §11 · **Status:** open · **Impact:** high

§9 states the constraint plainly: the developer is **two hours away**. §10 then describes a pipeline that overwrites the running application on every push, with no way back.

If a deploy breaks production — a bad build, a config error, an unexpected runtime failure — the factory is stopped and the only remedy described is another push, which requires diagnosing the fault remotely and getting the fix right first time. Meanwhile three production lines are unable to record work, and Part 1 §5.3 means they cannot legitimately work around it.

Three cheap mitigations, in order of value:

- **Keep the previous publish.** Deploy into a timestamped folder and switch to it, keeping the last few. Rollback becomes pointing back at the previous folder and restarting — a minute's work over RDP, no rebuild, no GitHub.
- **Deploy on demand, not on every push.** A `workflow_dispatch` trigger rather than push-to-deploy means a commit never reaches the factory unintentionally, which matters when the branch is also where work-in-progress lives.
- **Smoke check after restart.** Hit a health endpoint before declaring success, so a failed deploy is detected by the pipeline rather than by an operator holding a roll.

Rollback is harder once a migration has run — a schema change usually cannot be reversed without data loss. That is the strongest argument for Q86's backup-before-migrate rule.

---

## Q88 — HTTPS is listed as a future improvement
**Source:** Part 11 §19; Part 10 §2; Part 1 §6 · **Status:** open · **Impact:** high · *Escalates Q82*

§19 places "HTTPS Certificates" among optional items to add after v1. Combined with Part 10 §2, that means v1 ships JWT tokens and login passwords over plain HTTP, across Wi-Fi, on shared tablets.

A captured token is a working login for its entire lifetime (Q80), and on a shared wireless network capturing one requires no special access. This is the one deferred item on the §19 list that materially weakens something v1 already claims to provide — Part 10 §1 opens by calling access control "extremely important".

Everything else on that list is genuinely optional for a first version. This is not, and it is also among the cheapest: a self-signed or internal-CA certificate, `UseHttpsRedirection`, and the certificate trusted on each tablet during setup — done once, while the tablets are being configured, rather than revisited on deployed devices later.

---

## Q89 — No logging, for a developer two hours away
**Source:** Part 11 §9, §19 · **Status:** open · **Impact:** high

"Logging Systems" and "Automatic Health Monitoring" are both deferred to §19. Together with §9's two-hour travel time, that means when something fails at 2am the only available diagnostic is an operator's description over the phone.

Structured logging to rolling files is roughly an hour of work — Serilog with a file sink, one line in `Program.cs` — and it is what makes remote support possible at all. Without it, every incident starts from nothing.

Worth logging from day one: unhandled exceptions with stack traces, every failed login, every request that returns 4xx or 5xx, and the start and outcome of each deployment and migration. Retention can be short (14 days) since these are diagnostics, not records.

A `/health` endpoint returning database connectivity is a further few minutes and gives both the deployment smoke check in Q87 and a way to answer "is the server up?" without a site visit.

This is the item on §19's list with the best effort-to-value ratio for this specific project, precisely because of the constraint stated in §9.

---

## Q90 — Deployments land straight on the only environment, during shifts
**Source:** Part 11 §8, §10, §17 · **Status:** open · **Impact:** medium

The pipeline runs developer → GitHub → factory server. There is no test or staging environment anywhere in the specification, so the first execution of any code against a realistic database is in production, on the machine three production lines depend on.

There is also no deployment window. §11's runner deploys whenever a commit arrives, so an application restart can land mid-shift while an operator is halfway through assigning bags to a pallet.

Two low-cost improvements: restore a recent backup onto the developer's own machine and run new code against a copy of real data before it ships — this catches migration problems on real rows, which is where they actually occur; and restrict deploys to between shifts, or make them manually triggered (Q87), so the timing is chosen rather than incidental.

**Needs from you:** is there a window when the factory is not producing — a night gap, a weekend day — that deployments could target?

---

## Q91 — Barcode images should not be stored
**Source:** Part 11 §2, §14; Part 8 §11 · **Status:** open · **Impact:** low

§2 lists "Barcode Images" under file storage. Storing an image file per barcode means one file per roll, per bag and per pallet — on the order of 150,000 files a year at a modest production rate, all of which must then be backed up and restored.

A barcode is a deterministic rendering of its value: the same input always produces the same image. Generating it on demand at print time (or rendering it client-side in the label view) removes the storage, the backup weight and any possibility of an image drifting out of step with its record. Nothing is lost, because the image contains no information the database does not already hold.

On the same subject, §14's sizing discussion is reassuring in the other direction: at roughly a few thousand rolls and a few hundred thousand bags a year, the actual database will be well under a gigabyte per year. Disk space is not a real constraint here — the growth worth watching is accumulated backup files (Q81 retention), not the data.

---

## Q92 — Timestamps have no timezone strategy
**Source:** Part 11 §2; Part 6 §10; Part 9 §11, §13 · **Status:** open · **Impact:** medium

Every table carries timestamps, shift boundaries depend on wall-clock time (Q42), and nothing specifies whether times are stored as UTC or local.

This is not academic with this stack. Npgsql 6 and later map `timestamp with time zone` strictly to UTC `DateTime` values and **throw** when handed a local or unspecified kind — a well-known migration pain point that will surface on the first insert if the decision is left implicit.

The recommended combination: store instants as `timestamptz` in UTC, convert to factory-local time only for display and reporting, and keep genuine calendar dates such as `ShiftReports.ProductionDate` as `date` with no time or zone at all. That last distinction matters for the night shift — the production date is a business fact about which shift a record belongs to, not a timestamp, and treating it as one is how a 00:30 roll ends up on the wrong day (Q42).

Also worth confirming the server and tablets agree on the time. Tablets showing a different clock will confuse operators reading their own recent entries, even though the stored values come from the server.

---

## Q93 — Building on the production server contradicts §7
**Source:** Part 11 §7, §11 · **Status:** open · **Impact:** medium

§7 states that "the source code does not need to exist on the production server" — a good principle. §11 then has the self-hosted runner download the source, build it and publish it *on that same server*.

That requires the full .NET SDK, a NuGet cache and a working copy of the source on the factory machine, and it means a compilation error or a NuGet outage becomes a production-server problem. Build output also competes for CPU with the running application, and if the frontend is built there too, Node and its dependencies join the list.

The alternative keeps §7's principle intact: build and publish on GitHub's hosted runner, upload the published output as an artifact, and let the self-hosted runner do nothing but fetch that artifact, swap folders and restart. The factory server then needs only the ASP.NET Core runtime, and a broken build never leaves GitHub.

This does require the runner to reach GitHub, which §1's "internet not required for daily operation" permits — daily *operation* is offline; deployment is not a daily operation.

---

## Q94 — How tablets address the server, and the Docker volume trap
**Source:** Part 11 §4, §12, §14 · **Status:** open · **Impact:** low

Two smaller deployment details worth settling early.

**Server address.** §4 shows tablets connecting to the server but not how they find it. If the frontend is configured with a raw IP address, a DHCP lease change or a NIC replacement silently breaks every tablet at once, and each must be reconfigured by hand. A static IP plus a hostname in the factory's DNS (or a hosts entry) makes that a one-place fix.

**Docker volumes.** §12 plans to containerize PostgreSQL and §14 puts the data on local storage. A containerized PostgreSQL without an explicit named volume or bind mount loses its entire data directory when the container is recreated — which happens on every image update. If Docker goes ahead, the volume mapping is the single detail that must not be got wrong, and it is worth testing a full container rebuild against a throwaway database before trusting it with production data.

---

## Q95 — Barcodes are Phase 14 but required from Phase 5
**Source:** Part 12 §13; Part 3 §7; Part 4 §2; Part 8 §1 · **Status:** open · **Impact:** high

The phase order puts "Barcode Printing and Scanning" at **Phase 14**, after every production module is built. But barcodes are not a finishing touch — they are how the earlier modules start:

- Phase 5 (Extruder) prints the roll barcode as part of creating a roll (Part 3 §7).
- Phase 7 (Thermo) *begins* by scanning that barcode; Part 4 §3 says the operator must never type the roll number.
- Phase 9 (Produced Bags) prints a barcode per bag automatically on creation.
- Phase 10 (Wooden Pallets) prints a pallet barcode and assigns bags by scanning them.

Building those four phases without barcodes means building temporary dropdown-and-type screens for each, then rewriting them at Phase 14 — and the temporary version is the one that would get demonstrated to the factory.

Suggested reordering: barcode **generation and printing** move to Phase 4½ (immediately before extruder production), since it is a small self-contained service — generate a unique value, render it, print a label. **Scanning** can stay late only if the intermediate screens accept a typed or pasted barcode, which is a one-field form rather than a throwaway UI.

Note also that Phase 4 (Inventory) precedes Phase 5 (Extruder), which is correct — withdrawal needs stock to exist.

---

## Q96 — Shift Reports and the audit log have no phase
**Source:** Part 12 §13, §15; Part 6 §9; Part 8 §14 · **Status:** open · **Impact:** medium

`ShiftReports` appears in §15 under Management and in the §2 scope list, but nowhere in the §13 phase order. It is a required foreign key on `Rolls` (Part 3 §9), `ThermoProductions`, `RecyclerProductions` and `PackagingMaterialConsumption` — so nothing from Phase 5 onward can be built without it. It belongs with Master Data (Phase 2) or immediately after, not implicitly somewhere in Phase 13.

The **audit log** has the same problem from the other direction: listed in §15 under Infrastructure, absent from both the phase order and the Part 9 schema (Q70). Because it is written by an interceptor rather than called explicitly, adding it late means back-filling nothing — every action taken before it exists is simply unlogged, including the pilot period when the factory is still learning the system and mistakes are most informative.

Both are cheap early and awkward late.

---

## Q97 — The Dashboard is in scope but never specified
**Source:** Part 12 §2, §13, §15 · **Status:** open · **Impact:** medium

"Dashboard" appears in the v1 scope list, in Phase 13 and in the final system overview — but across all twelve parts nothing says what it shows, who it is for, or what it refreshes from.

The reporting list in §7 is well specified by comparison, so this is a genuine gap rather than a wording issue. Given Part 1 §6, there are two different audiences with different needs: the administrator on a desktop, who wants today's output, stock below minimum and shift progress; and an operator on a tablet, who mostly needs their own station's current state.

Worth deciding what belongs on it before Phase 13, since "dashboard" can mean anything from four number tiles to a live production board — a difference of days versus weeks.

**Needs from you:** what would the factory manager want to see on one screen when they walk in each morning?

---

## Q98 — Nothing describes go-live: opening balances or existing stock
**Source:** Part 12 §13 (Phase 15), §17; Part 7 · **Status:** open · **Impact:** high

The plan ends at "Testing and Deployment" and §17 mentions "gradual rollout", but no part of the specification covers the transition from paper to system. On the first morning the factory will have:

- **Existing raw material stock** in the store. Inventory starts at zero unless opening balances are loaded, and Part 7 §17's never-negative rule then blocks the first withdrawal of the day. This needs either a bulk opening-balance import or a `Receive`/`Adjustment` movement per material, done as a physical stock count immediately before go-live.
- **Rolls already produced** and waiting for thermo, with no barcode and no record. Either they are consumed on paper during a transition period, or they are entered retroactively with their real production data as far as it is known.
- **Bags and pallets already built** and sitting in finished goods, likewise.
- **Recipe versions in use**, which must be entered as version 1.0 of each family before any roll can be produced (Phase 3 covers this, but the *current* percentages need capturing from the supervisor).

There is also no mention of **operator training** or of running paper and system in parallel for a period. Given Part 1 §5.3 makes the ERP mandatory for production steps, a rollout where the system is unfamiliar and unforgiving is the most likely way for it to be worked around in its first week — which sets the habit permanently.

**Needs from you:** is there a natural pause — a maintenance day, a shutdown, an inventory count — that go-live could be aligned with? A cold start with an empty inventory is far easier during a stoppage than mid-week.

---

## Q99 — §5's expansion path depends on Templates, which nothing records
**Source:** Part 12 §5; Part 2 §8; Part 4 · **Status:** open · **Impact:** medium · *Confirms Q24*

§5 sets out how meal containers get added: a new Product Type, a new Thermo Template (mold), new Recipe Versions — with no workflow or schema change. It is the clearest statement of the expansion strategy in the specification.

But it cannot be exercised as designed, because **no production table records which template was used** (Q24). Adding a "Meal Box" template row would have no effect on anything: the thermo production would not reference it, the produced bags would not derive their product type from it, and nothing downstream would distinguish a meal box from a plate except the recipe family's product type — which is a recipe classification, not a statement of what was actually formed.

This confirms Q24 is structural rather than cosmetic. Adding `TemplateId` to `ThermoProductions` is a single nullable column now, and it is what makes §5's promise true: the mold determines the product, so recording the mold is what lets a second product line exist.

---
---

# Issues introduced by the v2 ERD

See [erd-v2-review.md](erd-v2-review.md) for the schema as drawn.

---

## Q100 — Hand-rolled `Users.PasswordHash` replaces ASP.NET Identity
**Source:** ERD v2 (Users, Roles); Part 10 §2, §13 · **Status:** open · **Impact:** high

Part 10 §2 and §13 are explicit that authentication uses ASP.NET Core Identity and that Identity handles hashing, verification and security updates. The ERD instead draws a plain `Users` table with a `PasswordHash` column and a plain `Roles` table — a custom identity system.

Writing your own password storage means owning decisions Identity already makes correctly: which KDF and iteration count, per-user salting, constant-time comparison, rehashing when parameters change, lockout after failed attempts, and normalized email/username lookup. Each is easy to get subtly wrong and the failure is silent — the system works fine until credentials leak and turn out to be cheaply crackable.

Identity's own schema is close to what is drawn anyway: `AspNetUsers` already has `Id`, `Email`, `PasswordHash`, `LockoutEnabled`; `AspNetRoles` is `Id`/`Name`; `AspNetUserRoles` joins them. Adding `FullName` and `IsActive` to a class deriving from `IdentityUser` gives the diagram's shape with none of the cryptography to own.

Recommendation: keep the ERD's field list, implement it as `IdentityUser`/`IdentityRole`. Nothing else in the diagram changes — every `UserId` FK still points at `Users.Id`.

**Needs from you:** was this a deliberate move away from Identity, or shorthand for "users and roles exist"? If deliberate, what drove it?

---

## Q101 — One role per user
**Source:** ERD v2 (`Users.RoleId`); Part 10 §11; Part 5 §14 · **Status:** open · **Impact:** high

`Users.RoleId` is a single foreign key, so a person holds exactly one role. Part 10 §11 assumes otherwise — `[Authorize(Roles = "Admin,ThermoOperator")]` only makes sense where a user can hold several — and Identity models roles as many-to-many for the same reason.

In a factory this size, people cover more than one station. A quality controller who covers both extruder and thermo needs two roles. If packaging and warehouse become separate roles (Q77/A6), the worker who does both needs two. The workarounds are all bad: duplicate accounts per role, which destroys the audit trail; or a combined "Packaging+Warehouse" role, which multiplies as combinations grow.

`ShiftWorkers.RoleInShift` partly acknowledges this — it records the role a person worked *in that shift*, which only has meaning if a person can have more than one.

Recommendation: a `UserRoles` join table (Identity provides it). `RoleInShift` then references a role the user actually holds, rather than being free text.

---

## Q102 — `Templates` has been dropped entirely
**Source:** ERD v2; Part 2 §8; Part 9 §3, §15; Part 12 §5 · **Status:** open · **Impact:** high · *Supersedes Q24, Q99*

`Templates` appears in Part 2 §8 as the physical mold, in Part 9's master table list twice, and in Part 12 §5 as one of the three things a new product requires. It is absent from the v2 ERD.

Previously the problem was that the table existed but nothing referenced it. Now it does not exist, which makes Part 12 §5's expansion path — new product type, new mold, new recipe versions, no schema change — impossible as written. There would be nothing to add a mold *to*.

It also leaves two facts without a source. `ProducedBags.ProductTypeId` and `PlateSizeId` have to come from somewhere: today the only path is the recipe family's product type (a recipe classification, not a statement of what was formed) and the QC report's plate size. The mold is what physically determines both.

Recommendation: restore `Templates` (Id, Name, ProductTypeId, PlateSizeId, IsActive) and add a nullable `TemplateId` to `ThermoProductions`. Product type and plate size then flow from the mold that made them, and adding a meal-box line is genuinely a data change.

**Needs from you:** A8 — does the factory swap molds, and is the specific mold worth recording?

---

## Q103 — `WoodenPallets` stores counts the spec forbade
**Source:** ERD v2 (`WoodenPallets.BagCount`, `.PlateCount`); Part 5 §10 · **Status:** open · **Impact:** medium

Part 5 §10 is unusually direct: *"BagCount and PlateCount should NOT be permanently stored. These values can always be calculated… Avoid storing calculated values whenever possible."* The ERD stores both.

They are `COUNT(BagPalletAssignments)` and `SUM(ProducedBags.PlateCount)` for the pallet. Stored, they must be updated on every assignment and can silently disagree with the assignments — and the assignment rows are the ones with barcode evidence behind them.

Two coherent options: drop the columns and compute (matches the written spec, and a pallet holds ~15 rows so the count is trivial); or keep them as a deliberate cache, updated in the same transaction as the assignment, with the same reconciliation obligation as `MaterialInventory.CurrentQuantity` (Q56).

The first is better here — unlike stock levels, these are read rarely and never in bulk.

---

## Q104 — Bag assignment no longer records who did it
**Source:** ERD v2 (`BagPalletAssignments`); Part 5 §9, §14 · **Status:** open · **Impact:** medium

Part 5 §9 defined the table as Id, ProducedBagId, WoodenPalletId, **AssignedByUserId**, AssignedDate. The ERD keeps Id, BagId, WoodenPalletId, AssignedAt — the user is gone.

That removes the only reason the junction table exists. With a bag belonging to exactly one pallet, the relationship could otherwise be a plain `WoodenPalletId` FK on `ProducedBags`; the table earned its place by carrying who assigned and when.

It also breaks Part 5 §14's rule that every action records the user, for one of the two operations that section names explicitly ("packaging cannot assign a bag without scanning it"). Bag assignment is the most repeated manual action in the factory and now has no accountability at all.

Recommendation: restore `AssignedByUserId`, and add `ShiftReportId` for consistency with every other production record.

---

## Q105 — `RecipeVersions` has both `Status` and `IsActive`
**Source:** ERD v2; Part 2 §11; Part 12 §6 · **Status:** open · **Impact:** low

`Status` carries `Draft` / `Current` / `Archived`, and `IsActive` is a separate boolean. They encode the same fact, and can contradict it — `Status = Archived` with `IsActive = true` is representable and meaningless.

Part 12 §6 settled the rule: exactly one active version per family at a time. One column should express it. `Status` is the more expressive of the two, since it also distinguishes a draft from a retired version, which a boolean cannot.

Recommendation: keep `Status`, drop `IsActive`, and enforce the rule with a partial unique index:

```sql
CREATE UNIQUE INDEX ux_recipe_active
  ON "RecipeVersions" ("RecipeFamilyId") WHERE "Status" = 'Current';
```

---

## Q106 — `MovementTypes` has no direction
**Source:** ERD v2 (`MovementTypes`, `MaterialInventoryMovements`) · **Status:** open · **Impact:** high · *Resolves the shape of Q53*

Promoting movement types to a master table is the right move, but as drawn it is `Id, Name` only. Nothing says whether a `Consumption` decreases stock and a `Receive` increases it — that knowledge would live in application code, matched against type *names*, which the specification rules out everywhere else (Part 2 §4).

The fix is one column on the master table:

```
MovementTypes: Id, Name, Direction   -- +1 (in) or -1 (out)
```

The balance is then `SUM(Quantity * Direction)` with no name matching anywhere, adding a movement type becomes a data change, and `Quantity` can be constrained positive so a sign error is impossible to store.

`Adjustment` is the one type that goes both ways (Q53), so it needs splitting into `Adjustment In` and `Adjustment Out` — which is better practice regardless, since a stock count that finds less than expected is a different event from one that finds more.

---

## Q107 — No master table has a soft-delete flag
**Source:** ERD v2; Part 2 §4; Part 9 §14; Part 10 §16 · **Status:** open · **Impact:** medium

Part 2 §4 gives `Materials` an `IsActive` column, and Part 9 §14 and Part 10 §16 both require soft deletion on master data — Materials, RecipeFamilies, Templates, Colors — so that historical production records keep resolving.

In the ERD, `IsActive` survives only on `Users` and `RecipeVersions`. `Materials`, `Colors`, `ProductTypes`, `PlateSizes`, `MaterialCategories`, `Units` and `RecipeFamilies` have none.

Without it, a discontinued pigment or a retired plate size can only be deleted — which restrict-delete correctly prevents once any roll references it — so obsolete entries accumulate in every operator dropdown with no way to hide them. On a tablet, a picker that only grows is a real usability problem and a source of wrong selections.

Recommendation: `IsActive` on every master table, with pickers filtering on it and historical joins ignoring it (see Q73 for the EF Core query-filter trap).

---

## Q108 — `ThermoShiftSummary` stores derivable values plus a free-text field
**Source:** ERD v2; Part 6 §16; Part 5 §10; Part 6 §11 · **Status:** open · **Impact:** medium · *Supersedes Q45*

The shift summary is now a table. Three of its five fields are derivable from records in the same database:

| Column | Derivable from |
|---|---|
| `RollWeightUsed` | `SUM(RollTestReports.Weight)` for rolls consumed in the shift |
| `TotalPlateCount` | `SUM(ThermoTestReports.PlateCount)` for the shift |
| `LossPercentage` | `LossWeight` ÷ `RollWeightUsed` |

`LossWeight` is the one genuinely new measurement — if it is weighed rather than computed, it belongs here. Note it also overlaps `RecyclerProduction.ScrapWeight` on the same shift; whether thermo loss and recycler scrap are the same physical material needs stating, especially now that `RecyclerRunningWithThermo` implies they can run together.

`FinalProduct (text)` is the bigger concern. A free-text column describing what a shift produced cannot be grouped, filtered or reported on, and it duplicates information already held structurally (product type and plate size, via the bags). If it names a product it should be `ProductTypeId`; if it is a shift comment, `Notes` already exists.

**Needs from you:** is `LossWeight` physically weighed at end of shift, and what does `FinalProduct` record?

---

## Q109 — `SupervisorId` became `OperatorId`, and hours are derived
**Source:** ERD v2 (`ShiftReports`); Part 6 §10 · **Status:** open · **Impact:** low

Two smaller changes.

Part 6 §10 had `SupervisorId` on the shift report; the ERD has `OperatorId`. Since `ShiftWorkers` already lists everyone on shift with their role, the single FK presumably marks whoever is responsible for the report. If that is the supervisor, the name should say so — it is the one field identifying accountability for a whole shift, and Q78's missing supervisor role hangs on it.

`ActualProductionHours` is `ProductionEndTime − ProductionStartTime − DowntimeHours`, so it is a stored calculation of the kind Part 6 §11 rules out. `DowntimeHours` is a real input and worth having; the total is not.

---

## Q110 — Packaging consumption cannot post to inventory without hardcoded mappings
**Source:** ERD v2 (`PackagingMaterialConsumption`, `MaterialInventoryMovements`); Part 5 §16, §17; Part 7 §12 · **Status:** open · **Impact:** high · *Escalates Q31*

The ERD keeps fixed columns — `PlasticHoodCount`, `ShrinkCount`, `SmallBagCount`, `SmallBagWeight`, `BigBagCount`, `BigBagWeight`, `WoodenPalletCount`, `TapeCount` — and there is now a visible consequence.

`MaterialInventoryMovements` requires a `MaterialId`. The diagram's own note says current quantities are "updated by movements". But `PackagingMaterialConsumption` holds no `MaterialId` anywhere, so posting these consumptions to inventory requires code that maps each *column* to a material id — a hardcoded table of eight column-to-material pairs, which is exactly the name-driven identification Part 2 §4 forbids. Renaming a material, adding a second tape type, or seeding a database in a different order all break it silently.

The `RecipeIngredients` pattern already in this schema is the fix:

```
PackagingMaterialConsumptions          PackagingConsumptionLines
  Id (PK)                                Id (PK)
  ShiftReportId (FK)                     ConsumptionId (FK)
  RecordedByUserId (FK)                  MaterialId (FK)
  RecordedAt                             Quantity
  Notes                                  (unique: ConsumptionId + MaterialId)
```

The entry screen then lists every material in the Packaging category automatically, and each line posts a movement directly — no mapping code at all.

One detail the ERD adds that is worth keeping: bags are recorded as both a **count** and a **weight** (`SmallBagCount` / `SmallBagWeight`). If both are genuinely recorded, the lines table needs `Quantity` plus an optional `Weight`, or two rows against different units.

Also still unresolved from Q34: `WoodenPalletCount` is typed in here while every `WoodenPallets` row is itself one empty pallet consumed — the system can count them exactly, and the two figures will disagree.

---
---

# Issues from the real thermoforming shift report

Source: [source-form-thermo-shift-report.md](source-form-thermo-shift-report.md) — a completed form for shift A, 2 July 2026. Where the form and the written specification disagree, the form is what the factory does.

---

## Q111 — The report is departmental, not factory-wide
**Source:** Form title; Part 6 §17; ERD v2 · **Status:** open · **Impact:** high

The form is titled **تقرير الإنتاج اليومي لقسم التشكيل** — *Daily Production Report for the **Forming Department***. It covers thermo production, pallet building, packaging materials, electricity and the people on that line. It does **not** cover extruder production or the recycler, which must have their own forms.

Part 6 §17 and the v2 ERD both model `ShiftReports` as a single factory-wide parent owning rolls (extruder), thermo productions, recycler production and packaging consumption together. That does not match the paperwork: the factory keeps one report per department per shift.

The distinction has real consequences. Machine settings (`CycleTime`, `FeedDistance`, `MachineSpeed`) are thermo settings — on a factory-wide report they have no owner, which is what Q44 flagged from the other direction. Electricity is metered for this line. The people listed are this line's crew, not the factory's.

Two ways to model it:

- **`ShiftReports` gains a `DepartmentId` (or `ProductionLineId`)**, with one report per (date, shift, department). Machine settings, electricity and workers then belong to the department that recorded them, and each production record points at its own department's report. This matches the paper exactly.
- **Keep one factory-wide report** and move the department-specific fields into per-line child tables. More tables, and the shift report becomes a thin header.

The first is closer to the factory and to the ERD's existing shape — it is one extra column plus a `Departments` master table.

**Needs from you:** are there separate extruder and recycler forms like this one? If so, sending one of each would settle the whole shift-report model.

---

## Q112 — "5.2 pallets" does not follow from the bag counts
**Source:** Form, إجمالي عدد المشاتيح · **Status:** open · **Impact:** medium

The pallet total reads **5.2 طبليات** (5.2 pallets), but the pallet table lists 14 + 15 + 15 + 15 + 2 = 61 bags. At 15 bags per pallet that is 4.07 pallets; counting physical pallets on the floor gives 5 (three numbered, two partial). Neither is 5.2.

Possible readings, none certain from one form:
- "5 pallets and 2 bags left over", written with a decimal point as shorthand.
- A different capacity for this product — 61 ÷ 5.2 ≈ 11.7 bags per pallet.
- A count that includes something not shown, such as a pallet carried in from the previous shift.

This matters because it is the figure the factory reports as its pallet output, and because it interacts with Q36 (how partial pallets close) and Q119 (how many empty pallets were consumed).

**Needs from you:** what does 5.2 mean here? And is 15 bags per pallet fixed for every product and plate size?

---

## Q113 — Thickness is recorded as a range, not four measurements
**Source:** Form, سماكة الرول; Part 3 §12; ERD v2 · **Status:** open · **Impact:** medium

Part 3 §12 and the ERD both specify `Thickness1`, `Thickness2`, `Thickness3`, `Thickness4` and a calculated `AverageThickness`. The real form records a single cell: **`2.9-3.2`** — a min–max range.

Two possibilities, and they lead to different schemas. Either the four-point measurement happens on a *separate extruder roll-test form* and this thermo form only carries a summary range for reference (likely, since Part 4 §7 says roll dimensions come from the roll test report) — in which case the ERD is right and this cell is display-only. Or the factory never takes four readings, and the four columns are aspirational.

If the range is what is actually recorded, storing it as text loses the ability to compare or chart it; `ThicknessMin` / `ThicknessMax` as numbers would preserve the same information usefully.

**Needs from you:** the extruder's own roll test form would answer this immediately.

---

## Q114 — Several "measurement" columns are constant across all rolls
**Source:** Form, roll table · **Status:** open · **Impact:** medium

On all five rolls, four columns hold identical values: thickness `2.9-3.2`, total time `50`, plate weight `9 g`, bag weight `4.5 kg`. Only weight, length, bag count and plate count vary.

That pattern says these are **nominal product specifications** copied onto the form, not per-roll measurements. Two of them are demonstrably nominal: 500 × 9 g = 4.5 kg exactly, which is arithmetic rather than a weighing; and total time of 50 is recorded identically for a 254 m roll that yielded 4,500 plates and a 350 m roll that yielded 7,000 — the same duration for 55% more output is not plausible as a measurement.

This matters for data entry and for reporting. If they are nominal, they belong on the product or recipe as defaults, pre-filled and rarely touched, and no report should treat them as observations. If any of them *is* genuinely measured per roll, it should be typed each time and would be expected to vary.

**Needs from you:** which of plate weight, bag weight and total time are actually measured during the shift, and which are copied from the product standard?

---

## Q115 — Packaging quantities are fractional and carry both count and weight
**Source:** Form, المتعلقات المستخدمة للتغليف; Part 5 §15 · **Status:** open · **Impact:** medium

Part 5 §15 states plainly that "the factory does not currently measure partial consumption". The form disagrees: small bags **4.142857**, large bags **6.1** — alongside weights of 5.8 kg and 5.185 kg.

The fractional counts are evidently derived from the weights (a part-used pack expressed as a fraction of a full one), which means the factory *does* track partial consumption, by weighing.

Three consequences for the schema:

- Quantity must be **decimal**, not integer.
- Some materials record **both a count and a weight** (shrink: 1 count / 3 kg; small bags: 4.14 count / 5.8 kg), while others record count only (pallets 3, tape 2) and one records neither meaningfully (hood 0).
- The `PackagingMaterialConsumption` fixed-column design (Q31, Q110) now has to carry two values per material, which turns eight columns into sixteen. The lines table proposed in Q110 handles this with one extra nullable column and no schema change per material.

**Needs from you:** is the count derived from the weight, or are both recorded independently? If derived, only the weight and a pack size need storing.

---

## Q116 — Employee numbers and trainees
**Source:** Form, الرقم الوظيفي / المتدربين; ERD v2 (`Users`) · **Status:** open · **Impact:** low

Two small additions the form makes plain.

The operator is identified as **`EMP0006`** — the factory already has employee numbers, and that is how a person is identified on paper. `Users` in the ERD has Id, RoleId, FullName, Email, PasswordHash, IsActive but no employee number. It should have one, unique, and it is a better login identifier than email for factory staff who may not have company email at all.

The form lists **العاملين (workers)** and **المتدربين (trainees)** on separate lines. Trainees are present on shift and recorded, but presumably do not sign for production the way an operator does. `ShiftWorkers.RoleInShift` can carry this if trainee is one of its values — worth confirming rather than assuming, since it affects whether a trainee can be selected as an operator on a production record.

---

## Q117 — Shift summaries are per product, not per shift
**Source:** Form, المنتج النهائي; ERD v2 (`ThermoShiftSummary`) · **Status:** open · **Impact:** high

The summary block has **two rows** — one for `AB500`, one for `NOR500` — each with its own loss percentage, loss weight, roll weight used and plate count. The roll table carries the same split, with subtotal rows for `ABS-Big` and `NOR-BIG`.

The ERD models `ThermoShiftSummary` as one row per `ShiftReportId`. The form shows it is one row **per product per shift**, because a shift can run more than one product and the loss figures are only meaningful within a product.

This changes the relationship from 1→1 to 1→many and adds a product key. `FinalProduct` is that key — `NOR500` / `AB500` decoding as recipe family plus plates per bag, which is a real product identifier rather than the free text Q108 worried about.

It also means product and plate size need to be dimensions of the summary (`NOR-BIG` versus `ABS-Big` distinguishes both), so the summary is keyed by shift report + product type + plate size, or by a proper product code if the factory maintains one.

**Needs from you:** is `NOR500` / `AB500` an official product code list, and does it vary by plate size (is there a `NOR500` small as well as big)?

---

## Q118 — The subtotal cell reading 7 does not match five roll rows
**Source:** Form, roll subtotal row · **Status:** open · **Impact:** low

The `NOR-BIG` subtotal row carries **7** in the serial-number column, while the table above it lists five rolls. The weight, bag and plate subtotals on that row all reconcile exactly to those five rolls (445.5 kg, 61 bags, 30,500 plates), so the 7 is not a roll count of the rows shown.

It may count rolls started but not finished, rolls including two carried over, or be left over from a previous version of the sheet. Worth clarifying only because if it is a real count of rolls touched during the shift, then rolls can be partially consumed — which would contradict Part 4 §4's rule that a roll is processed exactly once and never split.

**Needs from you:** does a roll ever get partly used and returned, or is it always fully consumed in one thermo run?

---

## Q119 — Pallets consumed counts only the numbered pallets
**Source:** Form, المشاتيح = 3; pallet table · **Status:** open · **Impact:** medium · *Relates to Q34*

The packaging line reports **3** pallets consumed. The pallet table shows five groups of bags — three numbered (1, 2, 3) and two unnumbered partials of 14 and 2 bags.

So an empty wooden pallet is counted as consumed only when a pallet is *completed and numbered*. The partial groups either sit on a pallet that is not yet counted, or are stacked loose awaiting completion.

This is directly useful for Q34: the system should deduct an empty pallet when a pallet reaches `Ready` and gets its number, not when a `WoodenPallets` row is first created in `Building` status. That also gives the numbering rule — a pallet number is assigned on completion, which is why partials have none.

**Needs from you:** do the 14 leftover bags physically sit on a pallet while they wait, or on the floor?

---

## Q120 — Roll dimensions appear on the thermo form
**Source:** Form, roll table; Part 4 §7 · **Status:** open · **Impact:** low

Part 4 §7 is explicit that roll weight, length and thickness are *not* entered on the thermo report because they already exist in the roll test report, and that the ERP should display them automatically.

The paper form carries all three. That is consistent with the specification's intent — on paper the only way to "display automatically" is to write them down again — and confirms the operator genuinely needs them visible while working.

Recorded so the screen design keeps them: the thermo entry screen should show roll weight, length and thickness read-only, pulled from the roll test report, in the same table position the operator is used to. No schema change; a UI requirement drawn from real use.
