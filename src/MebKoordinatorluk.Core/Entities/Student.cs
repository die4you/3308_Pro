using MebKoordinatorluk.Core.Common;

namespace MebKoordinatorluk.Core.Entities;

public class Student : BaseEntity
{
    public string TcKimlikNo { get; set; } = string.Empty;
    public string? Iban { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int DepartmentId { get; set; } = 1;
    public int? ClassId { get; set; }
    public string Classroom { get; set; } = string.Empty; // e.g. "12-A"
    public string Grade { get; set; } = "12";
    public int? FieldId { get; set; }
    public string? FieldName { get; set; }
    public int? EnterpriseId { get; set; }
    public string? EnterpriseName { get; set; }
    public int? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public string InternshipDays { get; set; } = "Pazartesi,Salı,Çarşamba";
    public string DutyDays { get; set; } = string.Empty; // Koordinatör öğretmen görev günü
    public string FeeCategory { get; set; } = "kat1"; // kat1 (%30) veya kat2 (%50)
    public string EducationType { get; set; } = "Örgün Eğitim"; // Örgün Eğitim, İkinci Öğretim, MESEM vb.
    public DateTime? BirthDate { get; set; } // Doğum Tarihi
    public DateTime? SgkEntryDate { get; set; } // SGK İşe Giriş Tarihi
    public DateTime? SgkExitDate { get; set; } // SGK İşten Çıkış Tarihi
    public string? SgkPrimKodu { get; set; } // SGK Prim Kodu / Belge Türü (Örn: 7, 42, 49, 50, 22)
    public string Gender { get; set; } = "Erkek"; // Erkek, Kız
    public bool HasFamilyHealthInsurance { get; set; } = true; // Aile Sağlık Güvencesi Var mı?
    public string EducationPlace { get; set; } = "İşletmede (Staj)"; // İşletmede (Staj), Okulda
    public int? Age
    {
        get
        {
            if (!BirthDate.HasValue) return null;
            var today = DateTime.Today;
            var birth = BirthDate.Value.Date;
            int age = today.Year - birth.Year;
            if (birth > today.AddYears(-age)) age--;
            return (age >= 0 && age < 120) ? age : null;
        }
    }
    public string? Phone { get; set; }
    public string? ParentPhone { get; set; }
    public string? Notes { get; set; }
    public bool IsSurekliDevamsiz { get; set; } = false;
    public bool IsActive { get; set; } = true;

    // İSG (İş Sağlığı ve Güvenliği) Kayıtları
    public string IsgStatus { get; set; } = "Sertifikasız (Eksik)";
    public DateTime? IsgCertificateDate { get; set; }
    public string? IsgCertificateNo { get; set; }
    public string? IsgSecurityNo { get; set; }
    public bool IsIsgDocumentPrinted { get; set; } = false;

    // Navigation / Display Helpers
    public string? DepartmentName
    {
        get => FieldName;
        set => FieldName = value;
    }

    public string ClassName
    {
        get => Classroom;
        set => Classroom = value;
    }

    public string FullName
    {
        get
        {
            var combined = $"{FirstName} {LastName}".Trim();
            return string.IsNullOrWhiteSpace(combined) ? (StudentNumber ?? "Öğrenci") : combined;
        }
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var parts = value.Trim().Split(' ', 2);
                FirstName = parts[0];
                LastName = parts.Length > 1 ? parts[1] : "";
            }
        }
    }

    public string DisplayInfo => $"{StudentNumber} - {FullName} ({Classroom})";

    public List<string> GetDaysList()
    {
        if (string.IsNullOrWhiteSpace(InternshipDays)) return new List<string>();
        return InternshipDays.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(d => TurkishTextHelper.NormalizeDayName(d.Trim()))
                             .Where(d => !string.IsNullOrWhiteSpace(d))
                             .ToList();
    }

    public List<string> GetDutyDaysList()
    {
        if (string.IsNullOrWhiteSpace(DutyDays)) return new List<string>();
        return DutyDays.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(d => TurkishTextHelper.NormalizeDayName(d.Trim()))
                       .Where(d => !string.IsNullOrWhiteSpace(d))
                       .ToList();
    }
}
