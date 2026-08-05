namespace Colors.Domain.Enums;

/// <summary>
/// Specification section 7. A ticket is open from the moment material leaves the
/// store until the leftover has been weighed back in.
/// </summary>
public enum IssueTicketStatus
{
    Open = 1,
    Closed = 2,
}
