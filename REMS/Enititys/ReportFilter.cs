namespace REMS.Enititys;

/// <summary>Filter used consistently by the report screen, dashboard, and Excel export.</summary>
public sealed class ReportFilter
{
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int? AssignedEmployeeId { get; init; }
    public string? Status { get; init; }
    public string? Search { get; init; }
}
