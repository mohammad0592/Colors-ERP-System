# Source Document — Thermoforming Daily Production Report

**File:** `تقرير الإنتاج2-7-2026 thermoforming machine_014736.xlsx`
**Company:** شركة كلرز للصناعات الورقية و البلاستيكية — Colors Company for Paper and Plastic Industries
**Form title:** تقرير الإنتاج اليومي لقسم التشكيل (Thermoforming machine) — *Daily Production Report, Forming Department*
**Instance:** Shift **A**, **2 July 2026**, operator م. علي حمدان (EMP0006)

This is a real completed form. Where it disagrees with Parts 1–12 or the v2 ERD, **the form is the authority** — it is what the factory actually does.

---

## 1. Form structure as filled

### Header
| Field (Arabic) | Translation | Value |
|---|---|---|
| الرقم الوظيفي | Employee number | `EMP0006` |
| اسم المشغل | Operator name | م.علي حمدان |
| التاريخ و اليوم و الوردية | Date / day / shift | 2026-07-02, shift **A** |

### Machine settings and electricity
| Field | Translation | Value |
|---|---|---|
| قراءة عداد الكهرباء — بداية الشفت | Meter, shift start | 170,516 |
| قراءة عداد الكهرباء — نهاية الشفت | Meter, shift end | 171,340 |
| كمية الإستهلاك | Consumption | **824** (= end − start ✓) |
| الزمن المستغرق للتشكيل للدورة الواحدة | Time per forming cycle | **8 seconds** |
| مسافة التغذية | Feed distance | **1220 mm** |
| سرعة التشكيل المستخدمة | Forming speed used | **580 cycles/hour** |
| نوع المنتج و صنفه | Product type and category | صحن عادي كبير الحجم (NORMAL) — *Normal large plate* |

> تم تشغيل خط الثيرمو بالتزامن مع خط الريسايكل — *"The thermo line was run simultaneously with the recycle line."*
> This is the `RecyclerRunningWithThermo` flag from the ERD, confirmed as a real field.

### Hours and people
| Field | Translation | Value |
|---|---|---|
| ساعة بداية الإنتاج للشفت | Production start time | 08:00 |
| ساعة توقف الإنتاج للشفت | Production stop time | 16:00 |
| مجمل ساعات الإنتاج الفعلية | Total actual production hours | 8 |
| العاملين | Workers | علي ياغي، صدام نجوم، محمد حمدان (3 names) |
| المتدربين | **Trainees** | — (none this shift) |

### Roll consumption table — five rolls in one shift

| الرقم المتسلسل<br>Roll code | وزن الرول<br>Weight kg | طول الرول<br>Length | سماكة الرول<br>Thickness | الزمن الكلي<br>Total time | حجم الصنف<br>Size | أكياس<br>Bags | صحون<br>Plates | وزن الصحن<br>Plate g | الإمتصاص<br>Absorb % | وزن الكيس<br>Bag kg | REC.% |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 34 GN240626B | 82.5 | 350 | 2.9-3.2 | 50 | Big | 14 | 7000 | 9 | 0 | 4.5 | - |
| 10 YN310526A | 90.0 | 350 | 2.9-3.2 | 50 | Big | 12 | 6000 | 9 | 0 | 4.5 | - |
| 9 YN310526A | 90.5 | 254 | 2.9-3.2 | 50 | Big | 9 | 4500 | 9 | 0 | 4.5 | - |
| 4 YN310526A | 91.5 | 350 | 2.9-3.2 | 50 | Big | 12 | 6000 | 9 | 0 | 4.5 | - |
| 3 YN310526A | 91.0 | 350 | 2.9-3.2 | 50 | Big | 14 | 7000 | 9 | 0 | 4.5 | - |
| **ABS-Big subtotal** | 0 | | | | | 0 | 0 | | | | |
| **NOR-BIG subtotal** (A=7) | **445.5** | | | | | **61** | **30500** | | | | |

### Pallet building table

| رقم المسلسل للرول<br>Roll serial | رقم المشتاح<br>Pallet no. | عدد الأكياس<br>Bags | عدد الصحون<br>Plates | صنف المنتج<br>Product | ملاحظات<br>Notes |
|---|---|---|---|---|---|
| - | **-** | **14** | 7000 | NOR كبير - أخضر *(large, green)* | - |
| - | 1 | 15 | 7500 | NOR كبير - أصفر *(large, yellow)* | - |
| - | 2 | 15 | 7500 | NOR كبير - أصفر | - |
| - | 3 | 15 | 7500 | NOR كبير - أصفر | - |
| - | **-** | **2** | 1000 | NOR كبير - أصفر | - |

**Totals:** إجمالي عدد المشاتيح (طبليه) = **5.2 طبليات** · إجمالي عدد الأكياس = **61** · أجمالي عدد الصحون = **30,500**

### Product summary — one row per product

| | نسبة الفاقد<br>Loss % | الفاقد<br>Loss kg | وزن الرولات المستخدمة<br>Roll weight used | عدد الصحون<br>Plates | المنتج النهائي<br>Final product |
|---|---|---|---|---|---|
| | 0 | 0 | 0 | 0 | **AB500** |
| | **38.38%** | **171** | **445.5** | 30,500 | **NOR500** |

### Packaging materials — المتعلقات المستخدمة للتغليف

| | طربوش<br>Hood | شرينج<br>Shrink | أكياس صغيرة<br>Small bags | أكياس كبيرة<br>Large bags | المشاتيح<br>Pallets | شريط لاصق<br>Tape |
|---|---|---|---|---|---|---|
| العدد *(count)* | 0 | 1 | **4.142857** | **6.1** | 3 | 2 |
| الوزن كغم *(weight)* | 0 | 3 | 5.8 | 5.185 | - | - |

---

## 2. Arithmetic verified against the form

Every relationship below was checked against the actual numbers.

**Bags are exactly 500 plates each, on every roll.** 14→7000, 12→6000, 9→4500, 12→6000, 14→7000. The ÷500 rule holds at roll level in real production.

**Bag weight is consistent with plate weight.** 500 plates × 9 g = 4.5 kg = the recorded bag weight, exactly.

**Loss is calculated, not weighed:**
```
Loss kg  = RollWeightUsed − (PlateCount × PlateWeight)
         = 445.5 − (30,500 × 0.009 kg) = 445.5 − 274.5 = 171   ✓
Loss %   = 171 ÷ 445.5 = 0.38384 = 38.38%                       ✓
```

**Electricity:** 171,340 − 170,516 = 824 ✓

**Subtotals** reconcile: weights 445.5, bags 61, plates 30,500 all equal the sum of the five roll rows, and the pallet table's 14+15+15+15+2 = 61 bags / 30,500 plates matches.

---

## 3. Roll code format, decoded

`34 GN240626B` and `10 YN310526A` decode as:

```
34   GN 240626 B
│    │  │      └── Shift
│    │  └───────── Production date, DDMMYY
│    └──────────── Colour letter + recipe family
└───────────────── Serial number (space-separated)
```

- **G** = أخضر green, **Y** = أصفر yellow, **B** = black (per Part 3 §6's `13BABS240526A`)
- **N** = Normal, **ABS** = ABS
- Confirmed against the pallet table, which labels the green roll's output "NOR كبير - أخضر" and the others "أصفر".

**The date in the code is the extruder production date, not the thermo date.** Rolls made on 31/05 and 26/06 were consumed on 02/07 — so rolls sit in stock for **weeks**, and roll inventory is a real, long-lived balance rather than a same-day handoff.

---

## 4. What this form settles

| Question | Answer from the form |
|---|---|
| **A2 / Q21** — partial bags | No partials at bag level. Every roll yielded an exact multiple of 500. |
| **Q36** — partial pallets | **They happen and are recorded without a pallet number** (rows with 14 bags and 2 bags). Full pallets get numbers 1, 2, 3. |
| **Q108** — is LossWeight measured? | **No — it is computed** from roll weight minus plate output. Formula verified exactly. |
| **Q108** — what is `FinalProduct`? | A product code: `NOR500` / `AB500` = recipe family + plates per bag. Structured, not free text. |
| **Q30** — absorbent scope | 0 for NORMAL, and `REC.%` is `-` for non-black. Both are conditional on recipe family. |
| **Q62** — roll code format | The `13BABS240526A` convention from Part 3 §6 is the real one. Confirmed in production use. |
| **Q47 / Q109** — electricity, hours | Consumption is stored as end − start; the form stores the computed value. |
| ERD `RecyclerRunningWithThermo` | Real — appears as a printed sentence on the form. |
| Machine setting units | Cycle time **seconds** (8), feed distance **mm** (1220), speed **cycles/hour** (580). |

---

## 5. New questions from the form

Logged as **Q111–Q120** in [open-questions.md](open-questions.md).

- **Q111** — the form is *departmental*, not factory-wide; this contradicts the shift-report model.
- **Q112** — `5.2 طبليات` does not follow from 61 bags at 15 per pallet.
- **Q113** — thickness is recorded as a range (`2.9-3.2`), not four measurements.
- **Q114** — several "measurement" columns are constant; which are measured and which are nominal?
- **Q115** — packaging quantities are fractional and carry both count and weight.
- **Q116** — `Users` needs an employee number; trainees are tracked separately from workers.
- **Q117** — summaries are per product, so `ThermoShiftSummary` is one row per product, not per shift.
- **Q118** — the subtotal cell `A19 = 7` does not match the five roll rows.
- **Q119** — pallets consumed (3) counts only numbered pallets, not the two partials.
- **Q120** — roll dimensions appear on the thermo form despite Part 4 §7.
