using System.Collections.Generic;
using System.Threading.Tasks;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Models;

namespace MebKoordinatorluk.Core.Interfaces;

public interface ISgkCrossCheckService
{
    Task<SgkAuditReport> RunAuditAsync(
        int year,
        int monthIndex,
        List<Student> students,
        AppSettings settings,
        List<AttendanceEntry>? attendanceEntries = null);
}
