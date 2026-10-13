using Colors.Domain.Enums;

namespace Colors.Domain.Entities.Barcodes;

/// <summary>
/// Labels sent from a tablet to the label printer (specification section 19.13).
///
/// The tablets on the floor have no printer; the label printer hangs off one computer.
/// So a tablet does not print — it queues the labels here, and the computer's Print Queue
/// screen prints them, by itself when it is set up to or at the press of a button.
///
/// The labels are named by their barcodes, not copied: the label is drawn from the
/// object at the moment it is printed, so it shows what is true then.
/// </summary>
public class PrintJob
{
    public int Id { get; set; }

    /// <summary>One label each. A thermo run sends a dozen or more at once.</summary>
    public List<string> Barcodes { get; set; } = [];

    /// <summary>What the person sending it was told — "Roll 04N… logged".</summary>
    public string? Headline { get; set; }

    /// <summary>The screen it was sent from, so the computer can say where it came from.</summary>
    public string Source { get; set; } = string.Empty;

    public int RequestedByUserId { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public PrintJobStatus Status { get; set; } = PrintJobStatus.Pending;

    /// <summary>Who printed it or took it off the queue, and when.</summary>
    public int? DoneByUserId { get; set; }

    public DateTimeOffset? DoneAt { get; set; }
}
