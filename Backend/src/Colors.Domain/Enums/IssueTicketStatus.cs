namespace Colors.Domain.Enums;

/// <summary>
/// Specification section 7. A ticket is open from the moment material leaves the
/// store until the leftover has been weighed back in.
/// </summary>
public enum IssueTicketStatus
{
    Open = 1,
    Closed = 2,

    /// <summary>
    /// Issued by mistake. Everything on it went back to the store when it was cancelled
    /// (specification section 19.27), so it used nothing.
    /// </summary>
    Cancelled = 3,
}
