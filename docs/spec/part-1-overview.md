# Styrofoam Factory ERP System — Part 1: Overview, Workflow, Objectives

Version: 1.0

## 1. Introduction

**Project name:** Styrofoam Factory ERP System

Custom-built ERP for a single styrofoam manufacturing factory — not a general ERP (SAP/Odoo). The system matches this factory's exact workflow.

Current state: paper forms + Excel. Problems: missing production history, difficult inventory tracking, no traceability, human errors, material waste, difficult reporting, poor worker accountability.

Scope of v1: production management, inventory control, quality documentation, barcode traceability, recipe management, shift reporting.

**Explicitly out of scope for v1:** accounting, sales, customers, suppliers (possible future releases).

## 2. Factory Overview

Current product: **Styrofoam Plates**.
Future products: styrofoam meal boxes, other styrofoam packaging.

> Design constraint: the schema must support additional product types without redesign.

## 3. Production Lines

### 3.1 Extruder Line
Mixes raw materials per a recipe. Output = **Styrofoam Roll**.
Operator withdraws raw materials from inventory, prepares the recipe, starts production.

Each roll receives: Roll Number, Barcode, Production Date, Shift, Recipe Version, Color, Product Type.

Quality measurements are recorded immediately after production. **Documentation only** — they never approve or reject production. The factory always keeps the roll. Failed rolls may be donated, used as samples, or handled manually. Production is never blocked by a failed quality test.

### 3.2 Thermoforming Line
Consumes **one roll at a time**. A roll is never split across multiple thermo productions.

Thermo operator records: Time inside the thermo machine, Plate Size, Plate Count, Bag Count, Plate Weight, Bag Weight, Absorbent Percentage.

Roll measurements (weight, length, thickness) are NOT re-entered — they already exist on the Roll Test Report.

On completion the system automatically creates **Produced Bags**, each with: Unique Barcode, Weight, Plate Count, Color, Product Type, Production Date. Every bag is individually traceable.

### 3.3 Recycler Line
Collects remaining scrap after thermo production.

**Scrap is NOT tracked per roll.** Total scrap is weighed once at end of shift, so the recycler module stores **shift-level** data, not roll-level.

Recycler operator records: Total Scrap Weight, Loss Percentage, Produced Recycled Material Weight.

Output becomes inventory as **"Recycled Material"**, used mainly in Black recipes.

## 4. Product Flow

```
Receive Raw Materials
  ↓
Store in Inventory
  ↓
Extruder Production
  ↓
Create Roll
  ↓
Roll Test Report
  ↓
Thermo Production
  ↓
Thermo Test Report
  ↓
Create Produced Bags
  ↓
Assign Bags to Wooden Pallets
  ↓
Packaging
  ↓
Finished Goods Inventory
  ↓
Customer (future)
```

In parallel:

```
Thermo Scrap
  ↓
Recycler
  ↓
Recycled Material
  ↓
Raw Material Inventory
```

## 5. System Objectives

### 5.1 Production digitization
Replace all paper production reports. Every production event stored permanently.

### 5.2 Inventory control

**Raw materials:** GPPS, Recycled Material, Talc, Coloring Agents, Nucleating Agent, Absorbent Agent, Antistatic Agent.

**Packaging materials:** Tape, Shrink Wrap, Plastic Hood, Large Bags, Small Bags, Empty Wooden Pallets.

**Produced items:** Rolls, Produced Bags, Finished Wooden Pallets.

### 5.3 Worker accountability
Workers must not perform production steps outside the ERP. Every important operation requires: Login → Barcode Scan → System Confirmation.

### 5.4 Full traceability
Management must be able to answer: which recipe produced this pallet; which roll produced these bags; which shift/operator produced this roll; which thermo machine processed it; which recipe version; what quality measurements; which pallet contains this bag. Every product traceable backwards to raw materials.

### 5.5 Quality documentation
Quality checks are documentation, **not approval workflows**.

- **Roll Test Report:** Weight, Length, Thickness (4 measurements), Average Thickness.
- **Thermo Test Report:** Time Inside Machine, Plate Size, Plate Count, Bag Count, Plate Weight, Bag Weight, Absorbent Percentage.

### 5.6 Recipe improvement
Managers compare Recipe Version → Quality Results → Production Waste → Final Product Quality for continuous improvement.

## 6. Target Users / Environment

Factory-internal only. No internet requirement. Windows Server on-premise, users on the local network. Most users on **Android tablets**; only the Administrator uses a desktop.

## 7. User Roles (6)

| Role | Responsibilities |
|---|---|
| Administrator | User management, master data, recipes, reports, inventory adjustments, system configuration, database backup, barcode printing, production monitoring |
| Extruder Operator | Start production, select recipe, produce rolls, print roll barcodes |
| Extruder Quality Control | Record Roll Test Reports |
| Thermo Operator | Select rolls, record thermo production, create produced bags, print bag barcodes |
| Thermo Quality Control | Record Thermo Test Reports |
| Recycler Operator | Record recycler production and shift recycling statistics |

## 8. Technology Stack

- **Backend:** ASP.NET Core Web API
- **Frontend:** React
- **Database:** PostgreSQL
- **ORM:** Entity Framework Core
- **Auth:** ASP.NET Identity
- **Authorization:** role-based, `[Authorize(Roles="...")]`
- **Server:** Windows Server
- **Version control:** GitHub
- **Deployment:** publish ASP.NET application
- **Future:** Docker, self-hosted GitHub Actions, CI/CD

## 9. System Philosophy

- **Simplicity** — the system matches the factory workflow rather than forcing workers to change.
- **Traceability** — every important object has a unique identity and complete history (Roll → Bag → Pallet).
- **Barcode first** — barcodes are the primary navigation and tracking method; scan rather than type.
- **Immutable production records** — production history is never deleted; recipe versions are never edited, only superseded by a new version. Old rolls stay linked to the exact version used.
- **Future expansion** — schema supports future products (e.g. meal boxes) without redesign.

---
*End of Part 1. Part 2 defines master data: materials, product types, templates, units, colors, plate sizes, and the recipe versioning system.*
