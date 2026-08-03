# Styrofoam Factory ERP System — Part 10: Roles, Authentication, Authorization and Security

Version: 1.0

## 1. Introduction

Used only inside the factory by authorized employees. Since the system manages production, inventory, recipes and operations, access control is critical — every user may perform only operations related to their job.

Implemented with ASP.NET Core Identity and Role-Based Authorization. The objective is not only security but **accountability**: every important action is linked to the user who performed it.

## 2. Authentication

Every worker logs in before using the system. ASP.NET Core Identity stores password hashes — never plain text.

```
Username or Email + Password
  ↓
ASP.NET Identity validates credentials
  ↓
JWT Token is generated
  ↓
User accesses the ERP
```

## 3. Authorization

Role-Based Authorization, e.g. `[Authorize(Roles = "Admin")]`. Built into ASP.NET Core, so no custom permission system is needed in v1.

## 4. User Roles

1. Administrator
2. Extruder Operator
3. Extruder Quality Control
4. Thermo Operator
5. Thermo Quality Control
6. Recycler Operator

Additional roles can be added later without changing the database structure.

## 5. Administrator

Complete access: user management, master data, recipes, inventory adjustments, barcode printing, reports, database backup, system configuration, production monitoring.

**The administrator should be the only role allowed to create, edit or deactivate master data.**

## 6. Extruder Operator

**Can:** withdraw raw materials, select recipe version, create production rolls, print roll barcodes.

**Cannot:** modify recipes, edit inventory manually, view administrative reports.

## 7. Extruder Quality Control

**Can:** scan roll barcode, record Roll Test Report.

Cannot modify production information — only test measurements.

## 8. Thermo Operator

**Can:** scan roll barcode, start thermo production, record thermo production, automatically generate produced bags, print bag barcodes.

Cannot edit recipe information or inventory.

## 9. Thermo Quality Control

**Can:** record Thermo Test Report, document plate quality information.

Limited to quality documentation.

## 10. Recycler Operator

**Can:** record scrap weight, record recycled material produced, complete recycler report, submit recycler statistics.

## 11. Authorization Strategy

ASP.NET Core authorization attributes rather than custom permission tables:

```csharp
[Authorize(Roles = "Admin")]
[Authorize(Roles = "ThermoOperator")]
[Authorize(Roles = "Admin,ThermoOperator")]
```

## 12. User Management

Only administrators may create users, deactivate users, reset passwords and assign roles.

**Workers cannot register themselves. There is no public registration page.**

## 13. Password Security

Passwords are never stored in plain text, never recoverable, always hashed. ASP.NET Identity handles hashing, verification and security updates.

## 14. Audit Logging

Every important action records User, Date, Time, Action, Object.

Examples: Created Roll · Printed Barcode · Recorded Roll Test · Created Produced Bag · Assigned Bag To Pallet · Inventory Adjustment · Recipe Created · Recipe Version Created.

## 15. Data Integrity

All input validated: no negative inventory, no duplicate barcodes, no duplicate roll numbers, no invalid recipe references, foreign keys always valid.

## 16. Soft Delete Strategy

Production history is never deleted. Master data is deactivated: Material `IsActive = false`, Recipe `Archived`, Template inactive. Historical production records continue referencing them safely.

## 17. Backup Strategy

Daily, weekly and monthly backups. Each backup creates a separate file. **Backups must never overwrite previous backups automatically** — allowing restore to any previous point in time.

## 18. Recovery Strategy

If the server fails: install PostgreSQL → restore the latest backup → publish the ASP.NET application → reconnect the frontend. The system resumes without rebuilding the database manually.

## 19. Future Security Enhancements

Two-factor authentication · Active Directory integration · SSO · biometric authentication · tablet device management · login notifications · password expiration policies · session timeout configuration. Outside v1.

## 20. Module Summary

Authentication via ASP.NET Core Identity, authorization via built-in role-based attributes, no custom permission system in v1 — providing secure authentication, simple authorization, user accountability, reliable audit history and easy maintenance.

---

## Open questions raised during review

See [open-questions.md](open-questions.md): **Q77** (no packaging or warehouse role exists), **Q78** (Supervisor is referenced but is not a role), **Q79** (shared tablets undermine per-user accountability), **Q80** (JWT lifetime and revocation), **Q81** (backups live on the machine they protect), Q82–Q85.

---
*End of Part 10.*
