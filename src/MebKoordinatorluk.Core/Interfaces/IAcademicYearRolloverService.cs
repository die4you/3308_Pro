using System;
using System.Threading.Tasks;

namespace MebKoordinatorluk.Core.Interfaces;

public class AcademicYearRolloverOptions
{
    public string SourceYear { get; set; } = string.Empty;
    public string TargetYear { get; set; } = string.Empty;
    public DateTime TargetStartDate { get; set; }
    public DateTime TargetEndDate { get; set; }

    public bool PromoteClassesAndStudents { get; set; } = true;
    public bool ArchiveGraduated12thGrades { get; set; } = true;
    public bool ResetStudentEnterprisesForPromoted { get; set; } = false;
    public bool CopyTeachers { get; set; } = true;
    public bool CopyEnterprises { get; set; } = true;
    public bool CopyFields { get; set; } = true;
    public bool CopyAddressGroups { get; set; } = true;
    public bool ResetAttendancesAndPayrolls { get; set; } = true;
}

public class AcademicYearRolloverResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int PromotedStudentsCount { get; set; }
    public int GraduatedStudentsCount { get; set; }
    public int CopiedTeachersCount { get; set; }
    public int CopiedEnterprisesCount { get; set; }
    public int PromotedClassesCount { get; set; }
}

public interface IAcademicYearRolloverService
{
    Task<AcademicYearRolloverResult> ExecuteRolloverAsync(
        AcademicYearRolloverOptions options,
        IProgress<string>? progress = null);
}
