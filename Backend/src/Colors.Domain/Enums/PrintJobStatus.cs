namespace Colors.Domain.Enums;

/// <summary>Where a print job stands in the label printer's queue (specification section 19.13).</summary>
public enum PrintJobStatus
{
    /// <summary>Sent from a tablet, waiting for the computer with the printer.</summary>
    Pending,

    /// <summary>Printed at the computer.</summary>
    Printed,

    /// <summary>Taken off the queue without printing — sent twice, or by mistake.</summary>
    Cancelled,
}
