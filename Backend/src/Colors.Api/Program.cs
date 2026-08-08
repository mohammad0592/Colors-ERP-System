using Colors.Api.Auditing;
using Colors.Api.Extensions;
using Colors.Application.Common.Auditing;
using Colors.Infrastructure;
using Colors.Infrastructure.Persistence.Seed;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Before anything else, so a failure while the application is still starting is written
// down rather than lost (specification section 15).
builder.AddFileLogging();

// How a cloud host differs from the factory server: the port it chose, and a database
// address written its way. Both do nothing unless the host set them, so the factory
// server is untouched (specification section 15).
builder.UseHostPort();
builder.UseDatabaseUrl();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Database, Identity and everything else Infrastructure owns.
builder.Services.AddInfrastructure(builder.Configuration);

// The audit log needs to know who is acting, which only the web layer knows. Registered
// after AddInfrastructure so it replaces the "nobody" fallback (specification section 15).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, CurrentActor>();

// Refusals are written on their own scope, after the man already has his answer.
builder.Services.AddSingleton<RefusalLog>();

// Reading and checking the access token on every request.
builder.Services.AddJwtAuthentication(builder.Configuration);

// Lets the React development server reach the API. Not needed in production,
// where both are served from the same address.
builder.Services.AddFrontendCors(builder.Configuration, builder.Environment);

var app = builder.Build();

// Before the seeders, which write rows and therefore need the tables to exist. Does
// nothing unless Database:MigrateOnStartup is switched on — the factory server migrates
// deliberately, after a backup, through Migrate.ps1.
await app.MigrateIfAskedAsync();

// Roles and the first administrator. Safe to run every start — it only adds what
// is missing. Database migrations are NOT run here: they are a deliberate step in
// the deployment, taken after a backup (specification section 15).
await IdentitySeeder.SeedAsync(app.Services);

// The lines, shifts, units, colours and materials named in the specification.
// Real factory data, not demo data — it runs everywhere and only adds what is missing.
await MasterDataSeeder.SeedAsync(app.Services);

// The four recipes the factory gave us, each as version 1 and in production.
await RecipeSeeder.SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Demonstration accounts, one per job, so the system can be tried before any
    // real worker exists. They share one simple password, so this is fenced off
    // twice: only outside production, and only when Seed:DemoUsers is switched on.
    await DemoUserSeeder.SeedAsync(app.Services);

    // A way back in when the administrator password is lost — nobody else can reset
    // it. Fenced the same way: only outside production, and only when
    // Seed:ResetAdminPassword is switched on.
    await IdentitySeeder.ResetAdministratorPasswordAsync(app.Services);
}

// One line per request in the file log: who, what, the status and how long it took.
// Enough to answer "what was happening at 2am" without turning on anything else.
// Before everything that looks at the request, so they all see how it really arrived
// rather than how it looks after the host's router unwrapped it. Without this, behind a
// cloud host, the redirect below loops until the browser gives up.
app.UseProxyHeaders();

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseCors(CorsExtensions.PolicyName);

// The built screens, served by the API itself (specification section 15). One server,
// one address, so in the factory there is no second web server to start and no
// cross-origin request at all.
//
// Before authentication on purpose: the sign-in page cannot require a sign-in.
//
// In development this finds nothing and does nothing — there is no wwwroot, because the
// screens are being served by Vite on port 5173 with hot reload.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// A path under /api that matches no controller is a mistake in the caller, and it has to
// hear so. Without this the fallback below would hand it the sign-in page with a 200,
// and a screen would sit there trying to read HTML as JSON.
app.Map("/api/{**rest}", () => Results.NotFound());

// Everything else is a screen address. The React router runs in the browser, so the
// server has never heard of /reports or /production/pallets — it must return index.html
// and let the browser work out which screen that is. Without this, opening a link to a
// screen, or pressing refresh on one, gives a 404.
app.MapFallbackToFile("index.html");

app.Run();
