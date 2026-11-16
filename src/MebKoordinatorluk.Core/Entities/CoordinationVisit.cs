using MebKoordinatorluk.Core.Enums;

namespace MebKoordinatorluk.Core.Entities;

public class CoordinationVisit : BaseEntity
{
    public int AssignmentId { get; set; }
    public DateTime VisitDate { get; set; }
    public VisitStatus Status { get; set; } = VisitStatus.Yapildi;
    public string? FormNumber { get; set; } // İşletme Ziyaret / Rehberlik Formu Sıra No
    public string? ObservationNotes { get; set; } // Görüşülen yetkili, öğrenci durumu, tespitler
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Display & Joins
    public string? TeacherFullName { get; set; }
    public string? EnterpriseName { get; set; }
}
