using Colors.Application.Common.Auditing;

namespace Colors.Infrastructure.Persistence.Auditing;

/// <summary>
/// Nobody in particular — the fallback where there is no web request to ask.
///
/// The seeder, a migration and the tests all save through the same context, and the
/// audit interceptor must work there too rather than falling over. Those writes are
/// logged with no user, which is the truth: no worker did them.
///
/// The API layer registers a real one over this.
/// </summary>
public class NoActor : ICurrentActor
{
    public int? UserId => null;
}
