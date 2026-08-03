# Decisions Needed Before Implementation

The specification is complete (Parts 1–12) and [open-questions.md](open-questions.md) holds 99 review items. Most of those I can resolve myself while building. This page is the short list that I cannot.

They are split by who has to answer.

---

## A. Needs factory knowledge — only you can answer

These change the schema. Answering them costs a conversation; discovering them wrong after go-live costs a migration over production data.

### A1. Does the extruder operator mix one batch per roll, or one batch that yields several rolls?
**Blocks:** Q11, Q52, Q61, Q72 — the single most consequential open question.

Material withdrawals currently record shift, user and material — no roll, no recipe version. If it is one batch per roll, adding `RollId` makes per-roll consumption and yield exact. If a batch yields several rolls, the most that can be attributed is the recipe run, and three specified features have to be reworded to match: Part 7 §18's *Consumption By Recipe* and *Production Yield*, Part 8 §9's drill-down ending in "raw materials consumed", and Part 1 §5.4's traceability claim.

Either answer is workable. Silence is not — the schema has to commit one way.

### A2. What happens to the leftover plates at the end of a roll?
**Blocks:** Q21, Q36.

Part 4 §22 makes `Bag Count = Plate Count ÷ 500` a validated rule, which requires every roll to yield an exact multiple of 500. Partial bag, held over to the next roll, or scrapped? The same question applies one level up to a pallet that ends a run with fewer than 15 bags (Part 5 §19 says it can never become `Ready`).

### A3. How does the operator choose which pigment to use?
**Blocks:** Q2, Q13.

Recipe families 1 and 3 are "Except Black" with a generic `Coloring Agent` line, but a roll records a specific colour. Nothing links `Colors` to `Materials`, so the system cannot deduct the right pigment or say which one went into a roll. Does one colour always map to exactly one pigment material?

### A4. Are the additive percentages parts-per-hundred-resin?
**Blocks:** Q1.

Family 1 reads GPPS 100% + Talc 1% + Nucleating 1.5–2% + Coloring 1.5–2%, summing to ~104%. That is standard phr notation — polymer is the 100% base, additives are quoted against it. Confirming this determines whether `RecipeIngredients` needs a basis column and how kg quantities are calculated.

### A5. What are the shift names and clock times, and which date does a night shift belong to?
**Blocks:** Q42, Q43.

Also: does the supervisor open the shift in the ERP at the start, or does the system open one on first use? Nothing can post production without a shift report existing.

### A6. Who does packaging and warehouse work, and are there shift supervisors?
**Blocks:** Q77, Q78.

Part 5's entire module has no role authorized to perform it, and `ShiftReports.SupervisorId` references a role that does not exist in the six. Are packaging and warehouse one job or two?

### A7. Is a tablet assigned to a person or to a machine?
**Blocks:** Q79.

If to a machine, one login covers a whole shift and every record is attributed to whoever logged in first — an audit trail that is precise and wrong. Scanning a personal barcode to identify the actor fits the existing hardware and Part 12 §11's "without making daily work unnecessarily complicated".

### A8. Does the factory swap thermoforming molds, and is the specific mold worth recording?
**Blocks:** Q24, Q99, Q3, Q32.

`Templates` is currently referenced by nothing, which means Part 12 §5's plan for adding meal containers cannot actually be exercised. One nullable `TemplateId` on `ThermoProductions` fixes that and gives plate size and product type an authoritative source.

### A9. Is recycled material stored per shift, or tipped into one common bin?
**Blocks:** Q46, Q5.

Decides whether lot tracking is meaningful or fiction. If one bin, the honest choice is to state the traceability boundary in reports rather than pretend to a chain that does not exist.

### A10. Is there a stoppage — maintenance day, shutdown, stock count — that go-live could align with?
**Blocks:** Q98.

Inventory starts empty, and Part 7 §17's never-negative rule blocks the first withdrawal until opening balances are loaded. A physical count immediately before go-live is the natural way to do that, and it is far easier during a stoppage.

### A11. What should the factory manager see on one screen each morning?
**Blocks:** Q97.

"Dashboard" is in the v1 scope with no specification anywhere. The answer is the difference between four number tiles and a live production board.

### A12. Two smaller ones
- **What goes in a Small Bag?** (Q28) Tracked as packaging material, never consumed by anything in the spec.
- **How reliable is factory Wi-Fi near the extruder and thermo machines?** (Q66) Scanning is mandatory, so an outage stops production or drives it off-system.

---

## B. I can decide these — flagged so you can overrule

Recorded so nothing is silently chosen. Default action is in bold.

| Area | Decision | Entry |
|---|---|---|
| Cardinality | **Make six declared 1:1 relationships nullable-unique (0..1)** — a roll cannot be created before its test report otherwise | Q69 |
| Inventory | **Restrict `Inventory` to materials with a real `MaterialId` FK**; roll/bag/pallet stock becomes status-driven views | Q51, Q55, Q67 |
| Movements | **Signed quantities**, and add source-document links so a movement names the event that caused it | Q53, Q54 |
| Packaging | **Replace fixed material columns with a lines table**, mirroring `RecipeIngredients` | Q31 |
| Thermo | **Move `BagCount`/`PlateCount`/`BagWeight` to `ThermoProductions`** so the operator is not blocked by QC | Q22 |
| Bags | **Add `PlateSizeId` to `ProducedBags`** so pallet compatibility is checkable without the QC report | Q32 |
| Corrections | **Append-only reversals with reason** for bag assignments and measurement edits, never in-place updates | Q17, Q35, Q71 |
| Audit | **Application-wide audit log via an EF `SaveChanges` interceptor**, including shift and failed operations | Q63, Q70 |
| Barcodes | **One `Barcodes` table** owning global uniqueness and type resolution; reprints allowed and logged; manual entry allowed but marked | Q64, Q65 |
| Schema hygiene | **One timestamp naming convention; UTC `timestamptz`; `ProductionDate` as `date`** | Q74, Q92 |
| Security | **HTTPS in v1; short access token plus refresh; `IsActive` checked per request** | Q80, Q82, Q88 |
| Ops | **Serilog to rolling files and a `/health` endpoint from day one**; migrations as an explicit pipeline step with a backup taken first; deploy to timestamped folders for rollback | Q86, Q87, Q89 |
| Build order | **Barcode generation moves before Phase 5; shift reports to Phase 2** | Q95, Q96 |

---

## C. Recommended sequence

1. Answer section A (or as much of it as you can) — A1, A2 and A6 unlock the most.
2. I revise the schema against the answers and produce the EF Core model plus initial migration.
3. Build in the Part 12 §13 order, with the two corrections from Q95 and Q96.

Where an A-question stays unanswered, I will implement the option that is cheapest to change later, note the assumption in code and here, and keep moving rather than block.
