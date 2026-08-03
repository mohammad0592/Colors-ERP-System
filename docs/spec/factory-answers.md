# Factory Answers — Running Log

Answers from the factory, in the order received. These override the written specification where they disagree, because they describe what actually happens.

---

## Round 1

### Q1 — Does the extruder mix per roll or per batch?
**Status: waiting.** Being asked at the factory.

Blocks: Q11, Q52, Q61, Q72, A1. Decides whether per-roll consumption and yield are computable, or only per shift.

---

### Q2 — Who records roll measurements, and where? — **ANSWERED**

> "When the roll is produced the extruder operator takes the measurements and writes them down on a paper and after that they move it to an Excel file. It is measured once when it gets out from the extruder."

**What this settles:**

| Point | Answer |
|---|---|
| Who measures | The **extruder operator** — not a separate quality-control person |
| When | **Once**, as the roll comes off the extruder |
| Where now | Paper first, then copied into an Excel file |
| Is there an extruder record? | **Yes** — an Excel file exists. This is a second source document. |

**Consequences:**

1. **There is an extruder record after all.** Earlier assumption (only one report exists) was wrong — the thermo form is the only *printed form*, but the extruder keeps an Excel file. That file should answer Q113 (is thickness four readings or a range?), the roll code sequence, and how roll status is tracked between production and thermo. **Worth requesting.**

2. **Roll measurement is an extruder-operator task, not a QC task.** Part 1 §7 and Part 10 §7 define a separate "Extruder Quality Control" role whose only job is recording roll test reports. If the operator does it, that role may not exist in the factory — see follow-up F1.

3. **"Measured once" is confirmed**, which matches Part 3 §15's rule that roll measurements are recorded exactly once. Good — no re-measuring at thermo, so the thermo screen shows them read-only (Q120).

4. **Paper → Excel means double entry today.** The ERP removes that step, which is a concrete, easy win to show the factory early.

---

### Q3 — What does "5.2 pallets" mean, and is it always 15 bags?
**Status: waiting.** Being asked at the factory.

Blocks: Q112, Q36 (how a partial pallet closes), Q119 (when an empty pallet is counted as used).

---

### Q4 — How is the pigment chosen? — **ANSWERED, and it changes the withdrawal model**

> "The recipe doesn't decide the colour, so the inventory reduction will be based on the material withdrawal. Who decides what material to withdraw — the inventory manager. After he selects the items the system generates something like a ticket or invoice for the worker, and after that he will be able to get the material."

**What this settles:**

| Point | Answer |
|---|---|
| Who withdraws materials | The **inventory manager**, not the extruder operator |
| How the worker receives material | The system prints a **ticket / issue note**; the worker collects against it |
| How colour is chosen | The manager picks the specific pigment material at withdrawal time |
| What drives inventory reduction | The **withdrawal ticket**, not the recipe |

**Consequences — this is a significant change:**

1. **Part 3 §3 and Part 10 §6 are wrong.** Both give the *extruder operator* the job of withdrawing raw materials. In reality the operator does not touch the store; the inventory manager issues material to him.

2. **A new role is needed: Inventory Manager (storekeeper).** This is now the third missing role, with Packaging/Warehouse (Q77) and Supervisor (Q78).

3. **A new entity is needed: the withdrawal ticket.** This did not exist in any of the twelve parts or the ERD. It is a document with its own lifecycle — created by the manager, then collected by the worker — which means a status, and two different users on one record.

   ```
   MaterialIssueTickets              MaterialIssueTicketLines
     Id (PK)                           Id (PK)
     TicketNumber                      TicketId (FK)
     ShiftReportId (FK)                MaterialId (FK)
     IssuedByUserId (FK)   -- manager  Quantity
     CollectedByUserId (FK) -- worker   
     Status  (Draft / Issued / Collected)
     CreatedAt / CollectedAt
     Notes
   ```

4. **Inventory should reduce on collection, not on ticket creation.** Otherwise a printed-but-uncollected ticket lowers stock that is still physically on the shelf. The ticket also gives a natural barcode target — the worker scans the ticket to confirm collection, which fits the barcode-first philosophy and gives accountability at handover.

5. **The colour-to-material problem (Q2 / A3) is solved differently than expected.** No `Colors → Materials` mapping table is needed. The pigment is simply a material the manager selects, and the ticket line records exactly which one. The roll's colour and the pigment used are then linked *through the ticket*, not through master data.

6. **This may partly solve the traceability gap (Q11) at no cost.** If the ticket says what it is for — a recipe version, a colour, a production run — then material consumption is attributable to that much, even without per-roll withdrawal. See follow-up F3.

---

## Round 2

### Why material withdrawal exists — **ANSWERED**

> "The main objective of the material withdrawal is to control material usage. The workers are not careful about the material, so we made this approach… And after they take the material, there is always some remaining material that should go back to the inventory."

**Two consequences:**

1. **The purpose is waste control, not bookkeeping.** That changes what "good" looks like: the system's job is to show whether more material was used than the recipe required. See the variance approach below.

2. **Material returns are a real, routine event** — leftover material goes back to the store. No part of the specification or the ERD has this. Without it, stock will read low forever and every consumption figure will be overstated.

**Recommended model:**

```
MaterialIssueTickets              MaterialIssueTicketLines
  Id, TicketNumber                  Id, TicketId (FK)
  ShiftReportId (FK)                MaterialId (FK)
  IssuedByUserId (FK)               IssuedQuantity
  Status (Open / Closed)            ReturnedQuantity   -- filled later, may be 0
  CreatedAt, ClosedAt               (NetUsed = Issued − Returned, computed)
```

Two movement types on the ledger: `Issue` (−) and `Return` (+). Stock falls when the ticket is issued and rises again when leftovers come back, so `MaterialInventory` always matches what is physically on the shelf.

**The control report this enables** — the thing the factory actually wants:

| | |
|---|---|
| **Expected** | recipe percentages × rolls produced that shift |
| **Actual** | net used = issued − returned |
| **Variance** | the difference, per material, per shift |

A shift that used 15% more GPPS than the recipe called for becomes visible the next morning. That is stronger control than a signature at the store door, and it needs no extra work from the worker.

---

### Extruder roles — **ANSWERED**

> "There is no quality man. The person who works in the extruder takes the measurements, but we want to make them two separate roles — the operator and the test man… for now they have one person to do both jobs, but in future maybe add another one. So to avoid changing, we made them two roles and give the person both."

**Settles two things:**

1. **Rename the role.** "Extruder Quality Control" → **Test / Inspection person**. It is not a quality-approval role; it records measurements.

2. **Q101 is confirmed as a real blocker.** The factory explicitly wants *one person holding two roles*. The v2 ERD's `Users.RoleId` is a single foreign key and cannot express that. A `UserRoles` join table is required — this is now a factory requirement, not a preference.

---

### The Roll Log web app — **NEW SOURCE**

A temporary web app the user's brother built for the factory. Header: `TRIM PRESS · AUTO CUTTING LINE`. Footer: "Shared cloud log · synced live for everyone with the password". Has "Export to Excel & Reset".

**Fields on the entry form:**

| Field | Note |
|---|---|
| Number | Auto serial — **resets every day** |
| Roll Number | |
| Recipe | |
| Roll Color | |
| Date | |
| **Out Time** | Time the roll left the extruder — *not in the ERD* |
| Roll Weight | |
| Roll Length | |
| **Plate Weight** | Recorded at the extruder — *not in the ERD's RollTestReports* |
| **Thickness: RS, RM, LM, LS** | **Four readings**, by position across the roll |
| Average Thickness | **Calculated automatically** |

**What this settles:**

- **Q113 is answered: thickness IS four readings.** They are positional — RS, RM, LM, LS (right side, right middle, left middle, left side). Part 3 §12 and the ERD are correct. The `2.9-3.2` on the thermo form is just a min–max summary copied across. The four columns should be **named by position**, not `Thickness1..4`.
- **Q15 partly answered: the roll serial resets daily.** So `34 GN240626B` is roll 34 of 24/06/26. Uniqueness comes from serial + date, which is why the date is inside the code.
- **Rolls are processed out of order** — "based on customer demands", not first-in-first-out. Confirms roll stock is a real queue the thermo operator chooses from.
- **Roll status becomes `Available` at production**, ready for thermo. Confirms the v2 ERD status list and Q14.
- Two fields to add: **`OutTime`** and **`PlateWeight`** on the roll record.

---

### Material withdrawal units — **ANSWERED**

> "The material withdrawal will be in Kg if possible. For other types of material, as it requires — in piece or whatever it requires."

Confirms Q18 / Q60: the unit comes from the material's own `UnitId`. No unit choice at withdrawal, no conversion table.

---

### Inventory manager — **ANSWERED (new role, does not exist yet)**

> "They don't have an inventory manager. After the system is done they will hire one, or make one of the current workers an inventory manager… maybe two inventory managers, each takes a shift."

This role is new to the factory, so it can be designed freely. Assume one per shift, covering all shifts.

> "No, [the worker does not sign]. Just takes the material, but based on the shift and date we will know who takes the material."

So there is **no individual accountability at collection** — attribution is by shift. The ticket therefore needs `IssuedByUserId` (the manager) and `ShiftReportId`, but no collector. Stock moves once, at issue.

---

### Packaging — **ANSWERED, and the paper form supports it**

> "I really don't know, but why ask this question? We don't care who does the operation, we just want to store the result — the material consumption."

**This is correct, and the real form proves it.** On the thermoforming report, the pallet table *and* the packaging materials table are both part of the **thermo department's** report. Packaging is not a separate department with its own paperwork.

**So Q77 mostly dissolves:** no separate Packaging or Warehouse role is needed. The thermo operator records pallets and packaging consumption, exactly as on paper today.

One consequence to confirm — see **F6** below: if nobody scans bags onto pallets, then individual bag barcodes may not be needed at all.

---

## Round 3

### F6 — Per-bag barcodes — **ANSWERED: yes, and it answers Q33 too**

> "No, we want to print a barcode for each bag, because the pallet contains plates with the same characteristics — size, colour and type (normal or absorbent) — and this will be done through the barcode. When the pallet is empty, the pallet will take the first scanned bag's characteristics."

**This settles three things at once:**

1. **Per-bag barcodes are required.** Bag-level scanning stays in scope. ~150,000 labels a year is accepted.

2. **Q33 is answered — the "inherit from first bag" option.** A pallet is created empty with **no** product type, colour, plate size or type. The first bag scanned onto it **sets** all of them; every later bag is checked against those values.

   So on `WoodenPallets` these columns must be **nullable until the first assignment, then locked**:
   ```
   ColorId, ProductTypeId, PlateSizeId, (Normal/Absorbent)  -- all NULL while Building & empty
   ```

3. **Q32 is confirmed and extended.** The compatibility check needs **four** attributes, not three:

   | Attribute | Where it must live |
   |---|---|
   | Plate size | `ProducedBags.PlateSizeId` ✔ already added in ERD v2 |
   | Colour | `ProducedBags.ColorId` ✔ |
   | Product type | `ProducedBags.ProductTypeId` ✔ |
   | **Normal vs Absorbent** | **missing everywhere** |

**The missing attribute.** "Normal or absorbent" is the ABS/NOR distinction — it comes from the recipe family, not from `ProductTypes` (which is Plate / Meal Box / Container). Today it is only reachable by joining Bag → ThermoProduction → Roll → RecipeVersion → RecipeFamily, which is four joins on every barcode scan.

This is the same distinction as `FinalProduct` on the paper form: **`NOR500`** vs **`AB500`**.

Recommended: an `IsAbsorbent` flag (or a `ProductVariantId`) on `RecipeFamilies`, copied onto `ProducedBags` at creation alongside the other three. The pallet check then becomes one comparison of four columns — fast enough to run on every scan.

---

### F7 — What is "Plate Weight" on the Roll Log? — **ANSWERED**

> "When the roll is produced they take a sample from it and make a plate to measure its weight."

So it is a **real measurement**, taken from a sample plate made from that roll, at the extruder. Not a nominal value.

This partly answers **Q114**. On the *thermo* form, plate weight was 9 g on all five rolls — so the thermo figure is likely copied from the product standard, while the *extruder* figure is measured per roll.

**Consequence for the loss calculation.** The form computes loss as `RollWeight − (PlateCount × PlateWeight)`. If plate weight is genuinely measured per roll at the extruder, the loss figure should use **that roll's** measured plate weight, not a shared 9 g. On this form every roll happened to be 9 g so it made no difference — but it will on a shift where they differ.

Raises **F10**: is the plate weight on the thermo form measured again, or copied from the roll?

---

### F8 — Are returned materials weighed? — **ANSWERED: yes**

> "Yes, they weigh the materials."

So `ReturnedQuantity` is a real weighed number, and the variance report works exactly:

```
Waste = (Issued − Returned) − (recipe % × rolls produced)
```

No estimation anywhere in the chain.

---

### Q1 — mix per roll or per batch?
**Still waiting.**

### F2 — Roll Log Excel export
**Still waiting** — user will try to obtain one.

---

## Round 4

### Q1 — Mix per roll or per batch? — **ANSWERED: one big batch**

> "One big batch."

**This is the decisive answer.** Material is mixed once and that mix produces several rolls.

**What is now impossible:**
- Per-roll material consumption
- Per-roll yield (kg in ÷ kg out)
- Part 8 §9's drill-down ending at "raw materials consumed" **for one roll**

**What is still possible, and should be built:**
- Consumption per **batch** — if the batch is recorded as an entity
- Consumption per **recipe version** — a batch is mixed to one recipe
- Consumption per **shift**
- The waste-control variance report (issued − returned vs recipe expectation)

**Consequence for the schema.** A `MaterialBatch` (or `MixingBatch`) entity is the right home for this: the mix is created once against a recipe version and colour, materials are consumed by it, and the rolls it produced point back to it.

```
Rolls → MaterialBatch → RecipeVersion + Colour + issued materials
```

This gives the strongest traceability the physical process allows: a roll cannot say *how much* GPPS it used, but it can name **exactly which mix** it came from, and that mix's materials are known. That is a much better answer than "materials withdrawn some time during this shift".

Open: how big is a batch, and does it stay inside one shift — see **F15**–**F17**.

---

### Q3 — "5.2 pallets" and "7" — **ANSWERED: both are mistakes**

> "5.2 is a mistake, also number 7 in the report is a mistake. For pallets they want int, not decimal — 5 pallets means 5 pallets. We will decide its status in another feature like empty, opened, completed and shipped."

**Settles Q112 and Q118** — both were human errors on the paper form, not hidden business rules. Good: no strange arithmetic to reproduce.

**Pallet count is an integer.** Never fractional.

**New pallet status list, replacing the one in Parts 5 and the ERD:**

| Status | Meaning |
|---|---|
| `Empty` | Created, no bags yet — characteristics not set |
| `Opened` | Has bags, not full — characteristics locked from the first bag (F6) |
| `Completed` | Full |
| `Shipped` | Left the factory |

This fits F6 exactly: `Empty` is the state in which colour, size and type are still NULL.

It also answers **Q36** — the two unnumbered groups on the paper form (14 bags and 2 bags) are `Opened` pallets. They are not a problem to be closed early; they simply stay open until filled. So a pallet number is assigned at creation, and completion is a status change, not a numbering event.

---

### F10 — Plate weight measured twice — **ANSWERED**

> "They take the plate weight two times: one from the extruder sample, and the other after thermo."

Both are genuine measurements at different stages, so both belong in the schema:

- `RollTestReports.PlateWeight` — from a sample plate made at the extruder
- `ThermoTestReports.PlateWeight` — measured after forming

Not duplication. Comparing the two is itself useful — a gap between the sample and the real output points at a forming problem.

---

## Source — Roll Log export (`roll_log_2026-08-02.xlsx`)

Three rows, exported from the temporary web app.

| Number | Roll Number | Recipe | Colour | Date | Out Time | Weight | Length | Plate Wt | RS | RM | LM | LS | Avg |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 01WN180726A | **8** | White | 2026-08-02 | 09:05 | 88 | 350 | 9.1 | 2.9 | 3.1 | 2.7 | 2.95 | 2.912 |
| 2 | 02WN180726A | **8** | White | 2026-08-02 | 09:25 | 88.5 | 350 | 8.7 | 2.6 | 2.8 | 2.9 | 2.8 | 2.775 |
| 3 | 03WN180726A | **8** | White | 2026-08-02 | 09:45 | **350** | 350 | 9.2 | 3 | 3.2 | 3.1 | 3.05 | 3.088 |

**What this shows:**

1. **Recipes are identified by NUMBER.** `Recipe = 8`. Not "Normal v1.2". The factory says "recipe 8". Parts 1–12 invented families with versions 1.0/1.1 — the real system may be a numbered list. **F18.**

2. **Roll code format refined.** `01WN180726A` — zero-padded serial, no space:
   ```
   01  W  N  180726  A
   │   │  │  │       └── Shift
   │   │  │  └────────── Date DDMMYY
   │   │  └───────────── Recipe family (N = Normal)
   │   └──────────────── Colour (W = White, G = Green, Y = Yellow, B = Black)
   └──────────────────── Daily serial, zero-padded
   ```
   The thermo form's `34 GN240626B` has a space; this has none. Minor inconsistency in hand-typed codes.

3. **The code's date does not match the entry date.** Code says `180726` (18 July); `Date` column says 2026-08-02. Probably test data with a default date — but worth confirming. **F19.**

4. **Plate weight really does vary per roll** — 9.1, 8.7, 9.2. Confirms F7/F10, and confirms the loss calculation should use each roll's own measured plate weight, not one shared figure.

5. **Row 3 contains a data-entry error:** roll weight `350`, identical to roll length `350`. The other rolls weigh 88 and 88.5 kg. The operator copied the wrong number and nothing stopped him.

   This is a concrete argument for **validation ranges** in the new system — a simple "roll weight must be between 50 and 150 kg" rule would have caught it at the moment of typing. Worth quoting to the factory as a real benefit.

6. **Average thickness = mean of the four readings**, rounded to 3 decimals. Verified on all three rows.

7. **A roll comes off every ~20 minutes** (09:05, 09:25, 09:45) — roughly 3 rolls/hour, ~24 per shift.

8. **The app records no operator and no shift** (shift is only inside the typed roll code). The new system will capture both automatically from the login and the shift report.

9. All values are stored as **text** in the export. The new system uses proper numeric types.

---

## Round 5

### Machines — **ANSWERED: one per line**

> "Three lines: line 1 is mixer and extruder, line 2 is thermo, line 3 is recycler… you can say one machine from each."

**No `Machines` table is needed for v1.** The line *is* the machine. `ProductionLines` (Extruder / Thermo / Recycler) is enough, and it doubles as the department key for shift reports (**Q111**).

This also settles **Q44** and **Q20**: the machine settings on the shift report (`CycleTime`, `FeedDistance`, `MachineSpeed`) belong to the thermo line, and there is only one, so no ambiguity remains.

---

### Recycler recording — **ANSWERED (image not received)**

> "They gather all the waste at the end of the shift and weigh it, after and before recycling… They take the percent of the waste and the weight for **each product (normal and absorbent)**, then weigh the produced material from recycling."

**Two weighings:** scrap in (before recycling), recycled material out (after recycling).

**Split by product — but the split is calculated, not weighed.** The image supplied is the **loss table from the thermo report**, not a separate recycler form:

| نسبة الفاقد Loss % | الفاقد Loss | وزن الرولات المستخدمة Roll weight used | عدد الصحون Plates | المنتج النهائي Final product |
|---|---|---|---|---|
| 0% | 0 | 0 | 0 | AB500 |
| 38% | 171 | 445.5 | 30,500 | NOR500 |

**Correction to an earlier assumption.** I previously wrote that `RecyclerProduction` should be one row per product per shift. That is wrong. The correct split is:

| Table | Grain | Source of numbers |
|---|---|---|
| `ThermoShiftSummary` | **one row per product per shift** | **calculated** — `roll weight − (plates × plate weight)` |
| `RecyclerProduction` | **one row per shift** | **weighed** — scrap in, recycled material out |

The per-product breakdown (normal vs absorbent) lives in the thermo summary because that is where it is computed. The recycler weighs the combined scrap before recycling and the recycled material after — totals only.

**The free accuracy check still works, and is now clearer:**

```
Sum of thermo calculated loss  (171 + 0 = 171 kg)
                  vs
Recycler weighed scrap for the same shift
```

A large gap means the plate weight is wrong, or scrap is being lost or mixed between lines. Neither number costs extra work — both are already recorded.

---

### Defective bags — **ANSWERED**

> "They will not be packaged and will go in the recycler."

So a defective bag:
- **is** created by thermo and gets its barcode (it exists, it was produced)
- is marked `Defective`
- is **never** assigned to a pallet
- goes to the recycler, so its weight becomes part of the shift's scrap

This closes the gap I raised earlier — there was no way for a bag to leave the system. Now there is, and it needs no new mechanism: the bag's status change is the record, and its weight already flows into scrap through the recycler weighing.

One consequence for the loss calculation: a defective bag's plates were produced but not sold, so `plates produced` and `plates packed` are different numbers. Worth keeping both visible so the loss figure is not mistaken for packing shortfall.

---

### F11 — Barcodes on incoming raw materials — **ANSWERED: no, not in v1**

Scope reduced. Barcodes in v1 cover **rolls, produced bags and pallets** only. `Materials.BarcodeTracked` stays in the schema as a future switch.

---

### F12 — Pallet capacity — **ANSWERED: exactly 15 bags**

Fixed. Still stored as configuration rather than hardcoded, so a future product (meal boxes) can differ.

---

### F15 — Batch size — **ANSWERED**

> "Yes, in kg. Maybe from 15–17 rolls per shift."

A batch is measured in kilograms and yields roughly 15–17 rolls, which is about one shift of extruder output. (The Roll Log shows a roll every ~20 minutes, so 15–17 rolls ≈ 5–6 hours of running.)

---

### F16 — One colour per batch? — **ANSWERED: no, colour changes mid-batch**

> "No, they can change colour in the middle."

**This changes the batch model.** A batch is **one recipe, many colours**.

Physically this makes sense: the base mix (GPPS, talc, nucleating agent) is prepared in bulk, and the colouring agent is fed separately at the extruder, so the operator can switch colour without stopping the batch.

Schema consequence:

```
Batch          →  recipe version + base materials      (no colour)
Roll           →  batch + colour                        (colour per roll)
Colour agent   →  consumed separately, not part of the batch mix
```

So `ColorId` stays on `Rolls` (as the ERD has it) and must **not** move to the batch. The pigment is issued and consumed on its own, which also fits the answer to Q4 — the inventory manager issues the specific pigment separately.

---

### F17 — Batch across shifts — **ANSWERED: no**

> "We said if the shift ends all the material will go back to the inventory."

**Every shift starts empty and ends empty.** Material issued during a shift is either consumed or returned before the shift closes.

This is a clean, strong rule and it makes several things simple:

- A batch never crosses a shift boundary → `Batch` belongs to exactly one `ShiftReport`.
- The waste variance is **exact per shift**, with no material carried forward to confuse it:
  ```
  Used this shift = Issued − Returned      (both weighed)
  ```
- Closing a shift has a real meaning and a real check: **every issue ticket must be closed** (returned quantity entered) before the shift can close. This gives Q43's shift lifecycle a genuine business rule instead of an invented one.

---

### F18 — Recipes — **ANSWERED: four families, many variations**

> "They have four main recipes… they also have other recipes that are just some modification to these four — changing some percentages. The factory is like a startup, he tries new recipes a lot."

**The family + version model in Parts 2 and 12 is correct.** Four families, each with many versions, and versions are created often.

The four families as confirmed (identical to Part 2 §13):

| # | Family | Formula |
|---|---|---|
| 1 | Normal Except Black | GPPS 100% · Talc 1% · Nucleating 1.5–2% · Colouring 1.5–2% |
| 2 | Normal Black | GPPS 65% · Recycle 35% · Talc 1% · Nucleating 1.5–2% · Black colouring 2–2.5% |
| 3 | ABS Except Black | GPPS 100% · Absorbent 3–4% · Colouring 1.5–2% · Antistatic 1.5–3% · Talc 1% |
| 4 | ABS Black | GPPS 65% · Recycle 35% · Absorbent 3–4% · Colouring 1.5–2% · Antistatic 1.5–3% · Talc 1% |

**But the Roll Log says `Recipe = 8`.** So the factory refers to a recipe by a **single number**, not by "family + version". Both are needed:

```
RecipeVersions
  Id
  RecipeNumber   ← 8   the number the factory says out loud, unique, never reused
  RecipeFamilyId ← 1   Normal Except Black
  VersionNumber  ← 3   third variation of that family
  Status         ← Current
```

The operator picks "recipe 8" from a list; the system knows it is version 3 of Normal Except Black and holds the exact percentages. Reports can group by family or by exact recipe.

**Because they experiment often, creating a new version must be quick** — an administrator or supervisor copies the current version, edits percentages, saves as a new number. That is a real usability requirement, not a nice-to-have.

---

### F13 / F14 — Material packaging and units — **ANSWERED (as a design)**

> "I really don't [know], but we can make a small table containing all the units and let the user decide which unit he receives the material in. I think there will be a specific table for this information — what are the materials, what is their basic unit, and each bag how much it contains in kg or something like this, to make conversion from units easier."

This is the right design, and it is what the original brief asked for ("1 pallet = N bags = N × W kg").

```
Materials                       MaterialPackagings
  Id                              Id
  Code, Name                      MaterialId (FK)
  BaseUnitId (FK)  ← kg           UnitId (FK)        ← bag / pallet / piece
  ...                             QuantityInBaseUnit ← 25 (kg per bag)
                                  IsDefaultReceiving
```

**Example — GPPS:** base unit kg · `bag = 25 kg` · `pallet = 750 kg`.

Receiving then works exactly as described: the storekeeper picks the material, picks the unit he is receiving in (pallet / bag / kg), types the quantity, and the system converts to the base unit for stock. **Stock is always held in the base unit**, so there is one number per material and no ambiguity.

This also answers **F14** — a material can have several packaging rows, so the same material arriving in 25 kg bags *and* 1000 kg big bags is just two rows. No schema change needed.

The actual numbers (which materials, bags per pallet, kg per bag) are **data**, to be entered by the factory when the system is set up. They do not need to be known now.

---

### F21 — Recycled material — **ANSWERED: one material**

> "One, and it is called recycled material, and goes back to the inventory after it is produced."

A single material, `Recycled Material`, increased by the recycler at end of shift. No split by normal/absorbent on the inventory side.

This confirms **Q46**: recycled stock is a single pooled balance with no lot identity. The traceability boundary is real and should be stated plainly in reports — a black roll traces back to "recycled material" as a material, not to the shifts that produced it.

---

## Open follow-ups

Only minor items remain; none block the design.

- **F3** — will the issue ticket name the recipe it is for? *(design choice)*
- **F9** — should "Recipe" be a dropdown of recipe numbers? *(almost certainly yes)*
- **F19** — why does the roll code say 18/07 when the entry date is 02/08? *(probably test data)*
- **F20** — is the batch weighed, or calculated from the materials issued to it?
