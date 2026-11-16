using System.Collections.Generic;
using System.Threading.Tasks;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Enums;

namespace MebKoordinatorluk.Core.Interfaces;

public interface ILicenseService
{
    string MachineCode { get; }
    LicenseInfo CurrentLicense { get; }
    Task<LicenseInfo> RefreshLicenseStatusAsync();
    Task<(bool Success, string Message)> ApplyLicenseKeyAsync(string licenseKey);
    bool CanAddStudent(int currentCount);
    bool CanAddEnterprise(int currentCount);
    string GetWatermarkText();
    Task<List<LicenseHistory>> GetLicenseHistoryAsync();
    Task RevokeLicenseToDemoAsync(string reason);
    AcademicYearAccessStatus GetAcademicYearAccess(AcademicYear year);
    AcademicYearAccessStatus GetAcademicYearAccess(string yearName, DateTime? startDate = null, DateTime? endDate = null);
}
