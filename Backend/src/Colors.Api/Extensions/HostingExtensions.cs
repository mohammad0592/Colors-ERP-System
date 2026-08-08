using Colors.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Colors.Api.Extensions;

/// <summary>
/// The three things a cloud host does differently from the factory server.
///
/// The factory server is the real home (specification section 15): one Windows machine,
/// its own database, its own certificate. A cloud host is only ever a guest house — a few
/// weeks so the factory can try the screens and say what is wrong before go-live.
///
/// Everything here is <b>inactive unless the host asks for it</b>. On the factory server
/// none of these settings exist, so none of this changes anything.
/// </summary>
public static class HostingExtensions
{
    /// <summary>
    /// Listens on the port the host picked.
    ///
    /// Cloud hosts hand the application a port in <c>PORT</c> and route traffic to it. The
    /// factory server has no such variable and keeps its own configured address.
    /// </summary>
    public static void UseHostPort(this WebApplicationBuilder builder)
    {
        var port = Environment.GetEnvironmentVariable("PORT");

        // ASPNETCORE_URLS wins if somebody set it deliberately.
        if (!string.IsNullOrWhiteSpace(port)
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
        {
            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        }
    }

    /// <summary>
    /// Accepts a database address written the way cloud hosts write it.
    ///
    /// They supply <c>DATABASE_URL</c> as <c>postgresql://user:password@host:port/name</c>,
    /// which Npgsql does not understand. This translates it into the settings it does,
    /// and only when no connection string was given the normal way — so the factory
    /// server's own setting is never overridden.
    /// </summary>
    public static void UseDatabaseUrl(this WebApplicationBuilder builder)
    {
        if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("ColorsDb")))
        {
            return;
        }

        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(url))
        {
            builder.Configuration["ConnectionStrings:ColorsDb"] = TranslateDatabaseUrl(url);
            return;
        }

        // Nothing anywhere. Stop here rather than three lines later in Infrastructure,
        // whose message is written for a developer at his own machine and tells a cloud
        // server to run `dotnet user-secrets` — a tool that does not exist there. The
        // message below is the difference between a twenty-second fix and an evening.
        throw new InvalidOperationException(NoDatabaseMessage(builder.Environment.EnvironmentName));
    }

    /// <summary>
    /// What to do when no database address was given, written for whoever is reading it.
    ///
    /// It lists the names of the environment variables that <i>are</i> present and look
    /// like they concern a database — names only, never values, because a log is not a
    /// place for a password. On a cloud host that list is usually the whole answer: it
    /// shows at a glance whether the database was connected to this service at all.
    /// </summary>
    private static string NoDatabaseMessage(string environmentName)
    {
        var candidates = Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Select(entry => entry.Key.ToString() ?? string.Empty)
            .Where(name => name.Contains("DATABASE", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("POSTGRES", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("PG", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("ConnectionStrings", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var found = candidates.Count > 0
            ? string.Join(", ", candidates)
            : "none — nothing here mentions a database at all";

        // Kept out of the interpolated text below: the braces in it are Railway's own
        // syntax, and doubling them to escape them makes the line unreadable in source
        // and easy to get wrong in a message whose whole job is to be copied correctly.
        const string railwayExample = "DATABASE_URL = ${{Postgres.DATABASE_URL}}";

        return $"""
            No database address was given, so the system cannot start.

            It looked for, in this order:
              1. ConnectionStrings:ColorsDb   (appsettings, or user-secrets in development)
              2. ConnectionStrings__ColorsDb  (an environment variable)
              3. DATABASE_URL                 (how cloud hosts usually give it)

            Database-related variables this process can actually see:
              {found}

            Environment: {environmentName}

            On a cloud host, an empty or unhelpful list above usually means the database
            was created but never attached to THIS service. A database's DATABASE_URL
            belongs to the database, not to the application — the application needs its
            own variable pointing at it. On Railway that is a variable reference such as:

                {railwayExample}

            On the factory server, set it for the whole machine (see
            docs/running-the-system.md, "First time on the factory server"):

                [Environment]::SetEnvironmentVariable('ConnectionStrings__ColorsDb', '...', 'Machine')
            """;
    }

    /// <summary>
    /// <c>postgresql://user:password@host:port/name</c> as Npgsql settings.
    ///
    /// Separate from the method above, and public, so it can be tested. The awkward part
    /// is the password: it is inside a URL, so anything awkward in it — an <c>@</c>, a
    /// <c>/</c>, a <c>#</c> — arrives percent-encoded and must be turned back. Get that
    /// wrong and the only symptom is a password failure against a database whose password
    /// is, as far as anybody can see, correct.
    /// </summary>
    public static string TranslateDatabaseUrl(string url)
    {
        var uri = new Uri(url);
        var credentials = uri.UserInfo.Split(':', 2);

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            // `Uri` reports -1 when the address named no port.
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : string.Empty,

            // Managed databases nearly always want TLS, and many present a certificate
            // signed by their own authority rather than a public one. `Prefer` uses TLS
            // where it is offered and still connects where it is not, which covers both
            // a hosted database and one sitting next to the application. It encrypts
            // without demanding a publicly trusted certificate — `VerifyFull` would be
            // the stricter choice, and the one to move to if this ever outlived the
            // trial and crossed a network somebody else can see.
            SslMode = SslMode.Prefer,
        }.ConnectionString;
    }

    /// <summary>
    /// Believes the host about how the request arrived.
    ///
    /// <b>Without this the site never loads at all.</b> A cloud host handles HTTPS at its
    /// own edge and passes a plain HTTP request inward. <c>UseHttpsRedirection</c> then
    /// sees HTTP, answers "go to HTTPS", the browser does, the host unwraps it to HTTP
    /// again — round and round until the browser gives up.
    ///
    /// The headers say what really happened outside, so the application knows the request
    /// was already secure and leaves it alone. It also puts the worker's real address in
    /// the log instead of the host's router.
    ///
    /// Only switched on when <c>Hosting:BehindProxy</c> is set, because believing these
    /// headers from anywhere else would let a caller claim to be someone they are not.
    /// </summary>
    public static void UseProxyHeaders(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Hosting:BehindProxy"))
        {
            return;
        }

        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,

            // The host's router is the only thing in front of the application, and its
            // address is not known ahead of time. Emptying these lists accepts the headers
            // from it. Safe only because this is switched on deliberately, for a
            // deployment where nothing but the host can reach the application.
            KnownIPNetworks = { },
            KnownProxies = { },
        });
    }

    /// <summary>
    /// Brings the database structure up to date, when — and only when — asked.
    ///
    /// Section 15 makes migrating a deliberate step taken after a backup, and that rule
    /// is not weakened here: this does nothing unless <c>Database:MigrateOnStartup</c> is
    /// switched on, and on the factory server it never is. There, <c>Migrate.ps1</c>
    /// remains the way, with a backup taken first.
    ///
    /// The trial is a different situation and the setting exists for it alone. A cloud
    /// container has no console to run a command in, the database behind it is empty on
    /// the first day, and everything in it is practice that will be thrown away. Making
    /// somebody find a way to open a shell against it would buy nothing.
    /// </summary>
    public static async Task MigrateIfAskedAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ColorsDbContext>();

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            app.Logger.LogInformation("Database is already up to date.");
            return;
        }

        // Named in the log, so what changed is answerable afterwards rather than being a
        // silent alteration to the one thing that holds the factory's records.
        app.Logger.LogWarning(
            "Applying {Count} database migration(s) on startup: {Migrations}",
            pending.Count,
            string.Join(", ", pending));

        await db.Database.MigrateAsync();

        app.Logger.LogInformation("Database migrations applied.");
    }
}
