# Running the System

Two programs must be running at the same time:

| | What | Address |
|---|---|---|
| **Backend** | ASP.NET Core API | http://localhost:5211 |
| **Frontend** | React (Vite) | http://localhost:5173 |

The frontend talks to the backend, so **the backend must be started first**.

---

## Every day: two terminals

### Terminal 1 — backend

```bash
cd "C:/Users/UnclePC/Documents/Projects/Colors ERP System/Backend" && dotnet run --project src/Colors.Api
```

Wait until it prints:

```
Now listening on: http://localhost:5211
```

### Terminal 2 — frontend

```bash
cd "C:/Users/UnclePC/Documents/Projects/Colors ERP System/Frontend" && npm run dev
```

Wait until it prints:

```
➜  Local:   http://localhost:5173/
```

Then open **http://localhost:5173** in the browser.

To stop either one, press `Ctrl + C` in its terminal.

---

## Or: one command

From the project folder:

```bash
powershell -ExecutionPolicy Bypass -File dev.ps1
```

This opens both in separate windows. Close the windows to stop.

---

## First time on a new computer

These are needed once, not every day.

**1. Install PostgreSQL 18** from postgresql.org — port `5432`, and write the password down.

**2. Tell the backend the database password:**

```bash
cd "C:/Users/UnclePC/Documents/Projects/Colors ERP System/Backend" && dotnet user-secrets set "ConnectionStrings:ColorsDb" "Host=localhost;Port=5432;Database=colors_erp;Username=postgres;Password=YOUR_PASSWORD" --project src/Colors.Api
```

**3. Set a key for signing login tokens** (any 32+ characters):

```bash
cd "C:/Users/UnclePC/Documents/Projects/Colors ERP System/Backend" && dotnet user-secrets set "Jwt:SigningKey" "change-this-to-any-long-random-text-32-chars-or-more" --project src/Colors.Api
```

**4. Set the first administrator password:**

```bash
cd "C:/Users/UnclePC/Documents/Projects/Colors ERP System/Backend" && dotnet user-secrets set "Seed:AdminPassword" "YourAdminPassword1" --project src/Colors.Api
```

Rules: at least 8 characters, one digit, one small letter.

**5. Create the database tables:**

```bash
cd "C:/Users/UnclePC/Documents/Projects/Colors ERP System/Backend" && dotnet ef database update --project src/Colors.Infrastructure --startup-project src/Colors.Api
```

**6. Install the frontend packages:**

```bash
cd "C:/Users/UnclePC/Documents/Projects/Colors ERP System/Frontend" && npm install
```

Now start both as above and sign in as `ADMIN001` with the password from step 4.

> Secrets are stored in your Windows user profile, **outside** the project folder, so they can never reach GitHub. Each computer needs its own.

---

## First time on the factory server

The server is **not** the same as a development computer, and one difference stops it dead: `dotnet user-secrets` does not exist there. It is a development tool that reads from your own Windows profile. A published build knows nothing about it.

So the two secrets have to be given to the server another way. Without them the API will not start at all — it checks on startup and says which one is missing, which is better than running and failing later on the first sign-in.

**1. Set the two settings for the machine**, in an administrator PowerShell:

```bash
[Environment]::SetEnvironmentVariable('ConnectionStrings__ColorsDb', 'Host=localhost;Port=5432;Database=colors_erp;Username=postgres;Password=YOUR_PASSWORD', 'Machine')
```

```bash
[Environment]::SetEnvironmentVariable('Jwt__SigningKey', 'a-long-random-text-of-at-least-32-characters', 'Machine')
```

Note the **two underscores**. That is how a nested setting is written as an environment variable: `ConnectionStrings__ColorsDb` is the same setting as `ConnectionStrings:ColorsDb` on your own machine.

`'Machine'` matters — it sets them for the whole computer, so the Windows service sees them. Set for your user only, they would work when you test by hand and vanish when the service starts.

**2. Say it is a production server:**

```bash
[Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Production', 'Machine')
```

This is what switches off the demonstration accounts and the administrator password reset. Both are fenced off outside development on purpose (specification section 3).

**3. Deploy, then create the database:**

```bash
.\deploy\Deploy.ps1
```

```bash
.\deploy\Migrate.ps1
```

**4. Install the service**, pointing at `current` and never at a dated folder — that link is what a rollback moves:

```bash
New-Service -Name ColorsErp -BinaryPathName 'C:\Colors\current\api\Colors.Api.exe' -DisplayName 'Colors ERP' -StartupType Automatic
```

After this the screens and the API are **one address**. The API serves the built screens itself, so there is no second web server to start, and nothing to configure for cross-origin requests.

> Changing an environment variable does not reach a service that is already running. After setting one, `Restart-Service ColorsErp`.

---

## After changing the database structure

When an entity changes, the database must be updated:

```bash
cd "C:/Users/UnclePC/Documents/Projects/Colors ERP System/Backend" && dotnet ef migrations add NameOfChange --project src/Colors.Infrastructure --startup-project src/Colors.Api --output-dir Persistence/Migrations
```

```bash
cd "C:/Users/UnclePC/Documents/Projects/Colors ERP System/Backend" && dotnet ef database update --project src/Colors.Infrastructure --startup-project src/Colors.Api
```

---

## Checking your work

```bash
cd "C:/Users/UnclePC/Documents/Projects/Colors ERP System/Backend" && dotnet build && dotnet test
```

```bash
cd "C:/Users/UnclePC/Documents/Projects/Colors ERP System/Frontend" && npm run lint && npm run typecheck && npm run build
```

All four must pass before anything is committed.

---

## When something goes wrong

| Problem | Cause | Fix |
|---|---|---|
| `Connection string 'ColorsDb' was not found` | Step 2 not done on this computer | Run step 2 |
| `The 'Jwt' configuration section is missing` | Step 3 not done | Run step 3 |
| Login page says **"Cannot reach the server"** | Backend not running | Start terminal 1 |
| Browser console shows a **CORS** error | Frontend is on a port the API does not allow | Vite must be on 5173. Close the other program using that port. |
| `Port 5173 is already in use` | An old Vite is still running | Close it, or restart the computer |
| `password authentication failed` | Wrong PostgreSQL password in step 2 | Run step 2 again with the right one |
| Backend starts but no admin exists | Step 4 not done | Run step 4, then restart the backend |

**To see which program is holding a port:**

```powershell
Get-NetTCPConnection -LocalPort 5173,5211 -State Listen | ForEach-Object { Get-Process -Id $_.OwningProcess | Select-Object Id, ProcessName }
```
