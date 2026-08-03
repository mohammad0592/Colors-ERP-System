# Styrofoam Factory ERP System — Part 12: Roadmap, Versioning Strategy and Final Summary

Version: 1.0

## 1. Introduction

The project follows an **incremental** strategy rather than building a massive enterprise solution up front. Version 1 covers only the factory's current workflow; further modules are added after the factory starts using the system and identifies real requirements. This reduces development time, lowers risk, and allows improvement based on real usage.

## 2. Version 1 Scope

User Authentication · Role-Based Authorization · Master Data Management · Recipe Versioning · Raw Material Management · Extruder Production · Roll Test Reports · Thermo Production · Thermo Test Reports · Produced Bags · Wooden Pallets · Packaging Material Consumption · Recycler Production · Shift Reports · Inventory Management · Barcode System · **Dashboard** · **Reports**

Designed to replace paper records and Excel sheets while improving traceability and inventory control.

## 3. Version 1 Business Goals

Replace manual paperwork · reduce dependency on Excel · track every production stage · improve inventory accuracy · increase worker accountability · improve recipe management · record production history · generate accurate reports · prepare for future automation.

## 4. Deferred to Future Versions

Customer Management · Supplier Management · Purchasing · Sales · Invoices · Accounting · Warehouse Locations · Shipping · Delivery Tracking · Production Scheduling · Machine Integration · IoT Sensors · OEE · Maintenance Management · Quality Analytics · Mobile Applications.

> These modules remain fully compatible with the current database design.

## 5. Future Product Expansion

The factory plans to manufacture styrofoam meal containers using new thermoforming templates. A new product requires:

- A new Product Type
- A new Thermo Template (Mold)
- New Recipe Versions (if needed)

The production workflow remains unchanged; the database needs no structural redesign.

## 6. Recipe Versioning Strategy

Four families — Normal (Non-Black), Normal Black, ABS (Non-Black), ABS Black — each with multiple versions (1.0, 1.1, 1.2, 2.0).

> **Only one version is active for production at any given time.** Older versions remain permanently to preserve production history.

## 7. Reporting Goals

Current Inventory · Inventory Movement History · Raw Material Consumption · Packaging Material Consumption · Production by Shift · Production by Operator · Production by Recipe · Recycler Efficiency · Roll Production History · Thermo Production History · Pallet Production · Recipe Usage · Future Customer Traceability.

These replace manual Excel calculations.

## 8. Barcode Philosophy

Not only identification but **process control**. Scanning wherever possible gives faster operation, fewer typing errors, accurate records and complete traceability.

## 9. Inventory Philosophy

Inventory should never be managed manually unless absolutely necessary — production modules update it automatically in every direction (extruder withdraws, recycler produces, packaging consumes).

## 10. Traceability Philosophy

```
Wooden Pallet → Produced Bags → Thermo Production → Roll → Recipe Version → Recipe Ingredients → Raw Materials
```

## 11. Worker Accountability

Improve accountability **without making daily work unnecessarily complicated**. Each important action records User, Date, Time, Shift.

## 12. Project Design Principles

Keep v1 simple · follow the factory's real workflow · avoid unnecessary complexity · design for future expansion · preserve historical data · minimize duplicate information · automate repetitive tasks · use barcodes for traceability · keep the database normalized.

## 13. Recommended Development Order

| Phase | Module |
|---|---|
| 1 | Authentication and User Management |
| 2 | Master Data |
| 3 | Recipe Management |
| 4 | Inventory Module |
| 5 | Extruder Production |
| 6 | Roll Test Reports |
| 7 | Thermo Production |
| 8 | Thermo Test Reports |
| 9 | Produced Bags |
| 10 | Wooden Pallets |
| 11 | Packaging Material Consumption |
| 12 | Recycler Module |
| 13 | Reports and Dashboard |
| 14 | Barcode Printing and Scanning |
| 15 | Testing and Deployment |

## 14. Success Criteria

Replace paper-based production records · replace Excel shift reports · track all recipes and versions · record production history · maintain accurate inventory · generate management reports · support barcode-based workflows · improve worker accountability · provide complete product traceability.

## 15. Final System Overview

**Master Data** — Materials, Units, Colors, Product Types, Plate Sizes, Templates, Recipe Families, Recipe Versions, Users

**Production** — Extruder, Roll Test Reports, Thermo, Thermo Test Reports, Produced Bags, Wooden Pallets, Packaging, Recycler

**Inventory** — Current Inventory, Inventory Movements

**Management** — Shift Reports, Dashboards, Reports

**Infrastructure** — Authentication, Authorization, Barcode System, Audit Logging, Backup Strategy, Deployment

## 16. Final Vision

The ERP becomes the factory's central information system — every production activity recorded digitally, real-time visibility for management, simple workflows matching how the factory already works, and complete traceability from raw materials to finished pallets.

## 17. End of Documentation

The next phase is implementation: database (PostgreSQL + EF Core), backend (ASP.NET Core Web API), frontend (React), testing, deployment and gradual rollout.

---

## Open questions raised during review

Resolves [Q6](open-questions.md) — §6 confirms one active version per family is a hard rule, not a norm.

New: **Q95** (barcodes are Phase 14 but required from Phase 5), **Q96** (Shift Reports and Audit Log have no phase), **Q97** (Dashboard is in scope but never specified), **Q98** (no go-live opening balances or data migration), **Q99** (§5's expansion path depends on Templates, which nothing records).

---
*End of Part 12 — end of specification.*
