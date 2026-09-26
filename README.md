# Colors ERP

Production and inventory system for **Colors Company for Paper and Plastic Industries**
(شركة كلرز للصناعات الورقية و البلاستيكية) — a styrofoam plate factory.

It replaces the paper shift reports: every roll, bag and pallet is recorded as it is made,
carries a printed barcode, and can be traced back to the batch and the raw materials it
came from.

---

## The factory

Three production lines, one machine each.

```
LINE 1 — Mixer + Extruder          LINE 2 — Thermoforming        LINE 3 — Recycler
raw materials → mix → ROLL    →    roll → plates → BAGS     →    scrap → RECYCLED MATERIAL
                                   bags → PALLET                          ↓
                                                                    back to inventory
```

1. The inventory manager issues raw materials to the extruder against a ticket.
2. The extruder mixes one batch to a recipe, producing 15–17 rolls in a shift.
3. Each roll is weighed and measured, and gets a barcode.
4. Rolls wait in stock, sometimes for weeks, and are used in the order the customer needs.
5. The thermo machine turns one whole roll into plates, packed into bags.
6. Bags are scanned onto wooden pallets. Every bag on a pallet must match.
7. Scrap goes to the recycler and comes back as material the store can use again.
8. A finished pallet is scanned onto the lorry when it leaves.
9. At the end of the shift, unused raw material is weighed back in.

---

## What it does

| Area | What it covers |
|---|---|
| **Shifts** | One record per shift for the whole factory — hours, crew, machine settings, electricity. Only one shift is open at a time. |
| **Inventory** | Material balances and every movement. Stock can never go negative. |
| **Material issue** | Tickets out to the line, and the weigh-back at the end of the shift. A ticket left open holds the shift open. |
| **Line 1 — Extruder** | Batches, rolls, and the roll test report (weight, length, plate weight, four thickness readings). |
| **Line 2 — Thermoforming** | Runs, what came out, and the bags created from the count. |
| **Pallets** | Scan a bag onto a pallet. The first bag decides what the pallet is; the rest must match. Each pallet takes a wooden pallet out of the store. |
| **Dispatch** | A finished pallet scanned onto the lorry. A wrong scan is undone with a reason. |
| **Packaging** | What the shift used, checked against what the counts say it should have used. |
| **Line 3 — Recycler** | How much recycled material the shift produced, back into stock. |
| **Recipes** | Families and versions, with percentages. Old versions are kept, never edited. |
| **Barcodes** | Printed for every roll, bag and pallet, and read from any screen — with a scanner, by typing, or with the tablet's camera. |
| **Reports** | Shift summary, material waste, packaging, pallets, recycled material. |
| **Dashboard** | Yesterday's production, what is waiting for somebody, and where money is leaking. |
| **Users** | Who may sign in and what each may do. One person can hold several roles. |
| **Audit log** | Who changed what, and what the system refused. |
| **Two languages** | Every screen in English or Arabic, and the whole layout mirrors for Arabic. The choice is remembered. |

---

## How it is built

**Clean architecture**, four projects, each depending only on the one before it:

```
Colors.Domain  →  Colors.Application  →  Colors.Infrastructure  →  Colors.Api
  entities,         interfaces and         EF Core, Identity,       controllers,
  the rules         the shapes             the implementations      authentication
```

The screens are a separate React application that talks to the API over HTTP.

| | |
|---|---|
| Backend | ASP.NET Core 10, EF Core 10, PostgreSQL, ASP.NET Identity, Serilog |
| Frontend | React 19, TypeScript (strict), Vite, Tailwind, React Router, TanStack Query |
| Languages | English and Arabic, right-to-left included |
| Database | 36 tables |
| Tests | 335 — 241 on the server, 94 on the screens |

A few rules the whole codebase follows, explained fully in the specification:

- **Calculate, or store?** A number is calculated when every input is on the row and
  frozen. It is stored only when an input is master data that can change later —
  otherwise last year's report would quietly rewrite itself.
- **Flags on rows, never name matching.** What a line can do is a tick box on the line,
  not a comparison against its name. Renaming "Extruder" must never break anything.
- **The ledger is the guard.** Stock cannot go negative, and that single rule is what
  protects the counts — not a separate check somewhere that could disagree with it.

---

## Running it

Full instructions, including first-time setup: **[docs/running-the-system.md](docs/running-the-system.md)**

Once set up, one command starts both halves in their own windows:

```bash
.\dev.ps1
```

| | |
|---|---|
| Screens | http://localhost:5173 |
| API | http://localhost:5211 |
| Is it alive? | http://localhost:5211/health |

The screens also open on a phone or tablet on the same network — useful, because that is
how the factory floor will actually use them.

---

## Tests

```bash
dotnet test Backend/Colors.slnx
```

```bash
cd Frontend && npm test
```

The server tests run against a real PostgreSQL database, created and thrown away on each
run — not an in-memory substitute, because the rules being tested are partial unique
indexes and constraints that only a real database enforces.

A test is only kept here once it has been made to fail. Writing a test that passes
against broken code proves nothing, so the rule is to break the thing on purpose first
and watch the test catch it.

---

## Deployment

**The factory server** is one Windows Server on the factory network. It serves the API and
the screens from a single address, so there is no second web server to start.

```bash
.\deploy\Deploy.ps1     # publish into a new dated folder and point 'current' at it
.\deploy\Migrate.ps1    # update the database — a deliberate step, after a backup
.\deploy\Rollback.ps1   # move 'current' back. Nothing is rebuilt
```

Nothing is ever overwritten. Each deployment is its own folder, so going back is switching
a link — one minute over remote desktop, which matters because the developer is two hours
away.

**The cloud trial** runs the same system as one Docker image, so the factory can try it
for a few weeks and say what is wrong before go-live. Try the image locally with:

```bash
docker compose up --build
```

Then open http://localhost:8080. Everything entered during the trial is practice and is
deleted before go-live.

---

## The documents

**[docs/specification.md](docs/specification.md)** is the authoritative document. It
describes what the factory does and why the system is built the way it is, and the code
follows it. When the two disagree, that is a bug in one of them.

| | |
|---|---|
| [docs/specification.md](docs/specification.md) | The whole design, in one file |
| [docs/running-the-system.md](docs/running-the-system.md) | How to run it, develop it, and deploy it |
| [docs/spec/](docs/spec/) | The working notes it was built from, and the factory's own answers |

---

## Where the project stands

Phases 1 to 13 are built and tested — everything in the table above works.

Phase 14 is the last one, and it is under way. The system is deployed to a rented server
so the factory can try it and say what is wrong. What remains after that is their
feedback, the real master data and opening stock, and confirming a few product numbers
that are currently sensible guesses — chiefly how many bags make a full pallet, which is
what decides when a pallet is finished.
