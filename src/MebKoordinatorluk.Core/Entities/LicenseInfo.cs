using System;
using MebKoordinatorluk.Core.Enums;

namespace MebKoordinatorluk.Core.Entities;

public class LicenseInfo : BaseEntity
{
    public string MachineCode { get; set; } = string.Empty;
    public string SchoolName { get; set; } = string.Empty;
    public string CityDistrict { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string LicenseKey { get; set; } = string.Empty;
    public DateTime? ActivatedAt { get; set; }
    public DateTime LastVerifiedAt { get; set; } = DateTime.UtcNow;
    public LicenseStatus Status { get; set; } = LicenseStatus.Demo;
    public string? Note { get; set; }

    public int RemainingDays
    {
        get
        {
            if (Status != LicenseStatus.Active || !EndDate.HasValue) return 0;
            var days = (EndDate.Value.Date - DateTime.Today).Days;
            return Math.Max(0, days);
        }
    }

    public bool IsActive => Status == LicenseStatus.Active && RemainingDays > 0;
}

public class LicenseHistory : BaseEntity
{
    public string MachineCode { get; set; } = string.Empty;
    public string SchoolName { get; set; } = string.Empty;
    public string LicenseKey { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
}
