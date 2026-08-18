using Colors.Application.Common.Auditing;
using Colors.Domain.Enums;

namespace Colors.Infrastructure.Persistence.Auditing;

/// <summary>
/// Nothing was scanned — the fallback where there is no web request to ask.
///
/// The seeders, the migrations and the tests all save through the same context, and the
/// audit interceptor has to work there too. The API layer registers a real one over this.
/// </summary>
public class NoEntry : ICurrentEntry
{
    public EntryMethod Method => EntryMethod.Unknown;
}
