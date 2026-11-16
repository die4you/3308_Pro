namespace MebKoordinatorluk.Core.Entities;

public class DocumentTrackingRecord : BaseEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int TeacherId { get; set; }
    public int EnterpriseId { get; set; }
    public bool TransitionStart { get; set; }
    public bool Week1 { get; set; }
    public bool Week2 { get; set; }
    public bool Week3 { get; set; }
    public bool Week4 { get; set; }
    public bool TransitionEnd { get; set; }
    public bool MonthlyReport { get; set; }
    public bool AttendanceSheet { get; set; }
    public string? Notes { get; set; }

    // Aliases for compatibility
    public bool TransitionStartDelivered { get => TransitionStart; set => TransitionStart = value; }
    public bool Week1Delivered { get => Week1; set => Week1 = value; }
    public bool Week2Delivered { get => Week2; set => Week2 = value; }
    public bool Week3Delivered { get => Week3; set => Week3 = value; }
    public bool Week4Delivered { get => Week4; set => Week4 = value; }
    public bool TransitionEndDelivered { get => TransitionEnd; set => TransitionEnd = value; }
    public bool MonthlyReportDelivered { get => MonthlyReport; set => MonthlyReport = value; }
    public bool AttendanceSheetDelivered { get => AttendanceSheet; set => AttendanceSheet = value; }
}
