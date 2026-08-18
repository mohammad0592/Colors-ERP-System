# Running the System

Two programs must be running at the same time:

| | What | Address |
|---|---|---|
| **Backend** | ASP.NET Core API | http://localhost:5211 |
| **Frontend** | React (Vite) | http://localhost:5173 |

The frontend talks to the backend, so **the backend must be started first**.

Every command below is run **from the project folder** — the one holding `Backend`,
`Frontend` and `docs`. They are written as relative paths on purpose, so they keep
working when the project is copied to another computer or another drive.

---

## Every day: two terminals

### Terminal 1 — backend

```bash
cd Backend && dotnet run --project src/Colors.Api
```

Wait until it prints:

```
Now listening on: http://localhost:5211
```

### Terminal 2 — frontend

```bash
cd Frontend && npm run dev
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
cd Backend && dotnet user-secrets set "ConnectionStrings:ColorsDb" "Host=localhost;Port=5432;Database=colors_erp;Username=postgres;Password=YOUR_PASSWORD" --project src/Colors.Api
```

**3. Set a key for signing login tokens** (any 32+ characters):

```bash
cd Backend && dotnet user-secrets set "Jwt:SigningKey" "change-this-to-any-long-random-text-32-chars-or-more" --project src/Colors.Api
```

**4. Set the first administrator password:**

```bash
cd Backend && dotnet user-secrets set "Seed:AdminPassword" "YourAdminPassword1" --project src/Colors.Api
```

Rules: at least 8 characters, one digit, one small letter.

**5. Create the database tables:**

```bash
cd Backend && dotnet ef database update --project src/Colors.Infrastructure --startup-project src/Colors.Api
```

**6. Install the frontend packages:**

```bash
cd Frontend && npm install
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

## The cloud trial

Before go-live the factory tries the system for a few weeks and says what is wrong. That happens on a rented server, not in the factory, because it must be reachable from anywhere and easy to throw away afterwards.

**This is a guest house, not the home.** The real system runs on the Windows Server in the factory. Nothing here changes that, and nothing here is needed for it.

**Everything in the trial is practice.** The factory should be told plainly: enter real work, try to break it, do not be careful. All of it is deleted before go-live.

### The image

The whole system is one Docker image — the API with the screens inside it, exactly as on the factory server. Build and try it on this machine first:

```bash
docker compose up --build
```

Then open `http://localhost:8080` and sign in as `ADMIN001`. Stop and throw the trial database away with:

```bash
docker compose down -v
```

### Putting it on a host

Any host that runs a Docker image will do — Railway, Render, Fly.io, or a plain server. Give it the image and these settings:

| Setting | What it is |
|---|---|
| `ConnectionStrings__ColorsDb` | The database. Most hosts instead give you `DATABASE_URL`, which the system reads by itself. |
| `Jwt__SigningKey` | Any 32+ random characters. Different from the factory server's. |
| `Seed__AdminPassword` | The first administrator's password. Everyone else is created from the Users screen. |

Two are already set inside the image and should be left alone: `Hosting__BehindProxy=true`, because every host handles HTTPS itself and the system has to be told so or the site never loads, and `Database__MigrateOnStartup=true`, because a container has no console to run migrations from.

Point the host's health check at `/health`.

### What the trial does not do

- **The data is not brought back.** Nothing copies the trial database onto the factory server. That is on purpose — the trial is practice.
- **Migrations run on startup here and nowhere else.** The factory server still uses `Migrate.ps1`, after a backup (specification section 15).
- **No demonstration accounts.** The image runs as Production, so `SUP001` and the rest do not exist. Real people, real accounts.

---

## After changing the database structure

When an entity changes, the database must be updated:

```bash
cd Backend && dotnet ef migrations add NameOfChange --project src/Colors.Infrastructure --startup-project src/Colors.Api --output-dir Persistence/Migrations
```

```bash
cd Backend && dotnet ef database update --project src/Colors.Infrastructure --startup-project src/Colors.Api
```

---

## Checking your work

```bash
cd Backend && dotnet build && dotnet test
```

```bash
cd Frontend && npm run lint && npm run typecheck && npm run build
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
