using Serilog;
using Serilog.Events;

namespace Colors.Api.Extensions;

/// <summary>
/// Logging to files, from day one (specification section 15).
///
/// The developer is two hours from the factory. Without a file on disk, a failure at 2am
/// can only be diagnosed by telephone — somebody reading an error message aloud over a
/// bad line while the extruder waits. This is the cheapest thing in the whole system
/// that prevents that.
///
/// The console keeps working as before, so nothing changes when running locally.
/// </summary>
public static class LoggingExtensions
{
    public static void AddFileLogging(this WebApplicationBuilder builder)
    {
        var folder = builder.Configuration["Logging:Folder"]
            ?? Path.Combine(AppContext.BaseDirectory, "logs");

        Directory.CreateDirectory(folder);

        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(folder, "colors-.log"),
                // A file a day, named by date, kept for a month. Long enough to answer
                // "what happened on Tuesday" and short enough that the disk on a factory
                // server never fills.
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                // One line per event, with the level and the time first, because it will
                // be read in Notepad over a remote desktop connection and nowhere else.
                outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Information,
                // Never let a full disk or a locked file stop the factory working.
                shared: true));
    }
}
