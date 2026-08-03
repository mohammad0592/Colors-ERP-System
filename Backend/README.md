# Colors ERP — Backend

ASP.NET Core Web API. See [`docs/specification.md`](../docs/specification.md) for what the system does.

## Structure

```
Backend/
├── Colors.slnx                  solution
├── Directory.Build.props        settings shared by every project
├── Directory.Packages.props     every NuGet version, in one place
│
├── src/
│   ├── Colors.Domain/           entities, enums, rules
│   ├── Colors.Application/      services, DTOs, interfaces
│   ├── Colors.Infrastructure/   EF Core, DbContext, repositories, Identity
│   └── Colors.Api/              controllers, middleware, Program.cs
│
└── tests/
    ├── Colors.UnitTests/        fast, no database
    └── Colors.IntegrationTests/ real database
```

## The one rule

Dependencies point **one way only**:

```
Api  →  Infrastructure  →  Application  →  Domain
```

| Layer | May reference | Must NOT know about |
|---|---|---|
| **Domain** | nothing | everything else |
| **Application** | Domain | EF Core, ASP.NET, PostgreSQL |
| **Infrastructure** | Application, Domain | ASP.NET |
| **Api** | all of them | — |

**Why `Application` must not reference EF Core:** business rules should not care whether data comes from PostgreSQL, a file, or a test double. `Application` declares an interface; `Infrastructure` implements it.

This is not a guideline — it is enforced by
[`tests/Colors.UnitTests/Architecture/LayerDependencyTests.cs`](tests/Colors.UnitTests/Architecture/LayerDependencyTests.cs).
Add a wrong reference and the test suite fails with a message naming the offending project.

Those tests read the **`.csproj` files**, not the compiled DLLs. The compiler strips references that are not used yet, so a DLL check would stay green until the day someone actually used the wrong thing.

## Where code goes

| Kind of code | Project | Folder |
|---|---|---|
| Entity (`Roll`, `Batch`, `ShiftReport`) | Domain | `Entities/<area>/` |
| Enum (`RollStatus`, `PalletStatus`) | Domain | `Enums/` |
| Business rule / use case | Application | `Features/<Feature>/` |
| Interface for storage or external services | Application | `Common/Interfaces/` |
| DbContext, entity configuration | Infrastructure | `Persistence/` |
| Repository implementation | Infrastructure | `Persistence/Repositories/` |
| Seed data | Infrastructure | `Persistence/Seed/` |
| Controller | Api | `Controllers/` |
| Middleware, DI wiring | Api | `Middleware/`, `Extensions/` |

Inside a layer, group by **feature**, not by technical type. All the material-issue code sits together, rather than being scattered across `Services/`, `Dtos/` and `Validators/`.

## Commands

```bash
dotnet build
```

```bash
dotnet test
```

```bash
dotnet run --project src/Colors.Api
```

## Conventions

- **Central package management.** Versions live only in `Directory.Packages.props`. A `.csproj` lists the package name with no version, so two projects can never drift onto different versions.
- **Nullable reference types are on**, and the nullable warnings that cause night-time crashes are errors.
- **Warnings become errors in Release**, so a broken build never reaches the factory.
- **Code style** comes from the root `.editorconfig` — file-scoped namespaces, braces always, `_camelCase` private fields.
