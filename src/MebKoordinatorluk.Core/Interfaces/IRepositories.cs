using MebKoordinatorluk.Core.Entities;

namespace MebKoordinatorluk.Core.Interfaces;

public interface ISettingRepository
{
    Task<IEnumerable<Setting>> GetAllAsync();
    Task<string?> GetValueAsync(string key);
    Task SetValueAsync(string key, string value, string? description = null);
    Task<AppSettings> GetAppSettingsAsync();
    Task SaveAppSettingsAsync(AppSettings settings);
    Task<IEnumerable<YearlyMinimumWage>> GetYearlyMinimumWagesAsync();
    Task<YearlyMinimumWage?> GetYearlyMinimumWageAsync(int year);
    Task<YearlyMinimumWage> GetEffectiveWageForYearAsync(int year);
    Task SaveYearlyMinimumWageAsync(YearlyMinimumWage wage);
    Task<bool> DeleteYearlyMinimumWageAsync(int year);
}

public interface IFieldRepository
{
    Task<IEnumerable<Field>> GetAllAsync();
    Task<Field?> GetByIdAsync(int id);
    Task<int> AddAsync(Field field);
    Task<bool> UpdateAsync(Field field);
    Task<bool> DeleteAsync(int id);
    Task<bool> DeleteAllAsync();
}

public interface ISchoolClassRepository
{
    Task<IEnumerable<SchoolClass>> GetAllAsync();
    Task<SchoolClass?> GetByIdAsync(int id);
    Task<int> AddAsync(SchoolClass schoolClass);
    Task<bool> UpdateAsync(SchoolClass schoolClass);
    Task<bool> DeleteAsync(int id);
    Task<bool> DeleteAllAsync();
    Task<bool> ExistsAsync(string name, string? fieldName, int? excludeId = null);
}

public interface IAddressGroupRepository
{
    Task<IEnumerable<AddressGroup>> GetAllAsync();
    Task<AddressGroup?> GetByIdAsync(int id);
    Task<int> AddAsync(AddressGroup addressGroup);
    Task<bool> UpdateAsync(AddressGroup addressGroup);
    Task<bool> DeleteAsync(int id);
}

public interface IDepartmentRepository
{
    Task<IEnumerable<Department>> GetAllAsync();
    Task<Department?> GetByIdAsync(int id);
    Task<int> AddAsync(Department department);
    Task<bool> UpdateAsync(Department department);
    Task<bool> DeleteAsync(int id);
}

public interface IAcademicYearRepository
{
    Task<IEnumerable<AcademicYear>> GetAllAsync();
    Task<AcademicYear?> GetActiveYearAsync();
    Task<AcademicYear?> GetByNameAsync(string name);
    Task<int> AddAsync(AcademicYear year);
    Task UpdateActiveYearAsync(string name);
    Task EnsureYearExistsAsync(string name, DateTime startDate, DateTime endDate);
}

public interface ITeacherRepository
{
    Task<IEnumerable<Teacher>> GetAllAsync();
    Task<Teacher?> GetByIdAsync(int id);
    Task<int> AddAsync(Teacher teacher);
    Task<bool> UpdateAsync(Teacher teacher);
    Task<bool> DeleteAsync(int id);
}

public interface IEnterpriseRepository
{
    Task<IEnumerable<Enterprise>> GetAllAsync();
    Task<Enterprise?> GetByIdAsync(int id);
    Task<int> AddAsync(Enterprise enterprise);
    Task<bool> UpdateAsync(Enterprise enterprise);
    Task<bool> DeleteAsync(int id);
}

public interface IStudentRepository
{
    Task<IEnumerable<Student>> GetAllAsync(int? departmentId = null, string? grade = null, bool includeInactive = false);
    Task<IEnumerable<Student>> GetArchivedStudentsAsync();
    Task<Student?> GetByIdAsync(int id);
    Task<int> AddAsync(Student student);
    Task<bool> UpdateAsync(Student student);
    Task<bool> DeleteAsync(int id);
    Task<bool> DeletePermanentlyAsync(int id);
    Task<bool> DeleteAllAsync();
    Task<bool> BulkAssignAsync(IEnumerable<int> studentIds, int? enterpriseId, int? teacherId, string? dutyDays);
    Task<bool> ClearAllAssignmentsAsync();
    Task<int> SyncClassInfoToStudentsAsync(SchoolClass schoolClass, string? oldClassName = null);
    Task<int> SyncClassInfoAndSgkCodesToStudentsAsync(SchoolClass schoolClass, string? oldClassName = null, bool updateSgkCodes = false, bool toMesem = false, bool updateField = false);
    Task<bool> ClearBulkSgkDatesAsync(IEnumerable<int> studentIds, bool clearEntry, bool clearExit);
}

public interface IAttendanceRepository
{
    Task<List<AttendanceEntry>> GetForMonthAsync(int year, int month);
    Task SaveEntryAsync(AttendanceEntry entry);
    Task SaveEntriesAsync(IEnumerable<AttendanceEntry> entries);
    Task BulkSetDayAsync(int year, int month, int day, string code, IEnumerable<int> studentIds);
    Task ResetMonthAsync(int year, int month, IEnumerable<Student> students);
    Task SyncClassAttendanceEntriesAsync(SchoolClass schoolClass, IEnumerable<Student> students, bool toMesem, AppSettings? settings = null);
}

public interface IDocumentTrackingRepository
{
    Task<List<DocumentTrackingRecord>> GetForMonthAsync(int year, int month);
    Task SaveRecordAsync(DocumentTrackingRecord record);
    Task BulkSetAllAsync(int year, int month, bool completed, IEnumerable<(int TeacherId, int EnterpriseId)> pairs);
}

public interface IStudentPlacementRepository
{
    Task<IEnumerable<StudentPlacement>> GetAllAsync(int? academicYearId = null);
    Task<StudentPlacement?> GetByIdAsync(int id);
    Task<int> GetStudentCountInEnterpriseAsync(int enterpriseId, int academicYearId);
    Task<int> AddAsync(StudentPlacement placement);
    Task<bool> UpdateAsync(StudentPlacement placement);
    Task<bool> DeleteAsync(int id);
}

public interface ICoordinatorAssignmentRepository
{
    Task<IEnumerable<CoordinatorAssignment>> GetAllAsync(int? academicYearId = null);
    Task<CoordinatorAssignment?> GetByIdAsync(int id);
    Task<double> GetTeacherTotalAssignedHoursAsync(int teacherId, int academicYearId);
    Task<int> AddAsync(CoordinatorAssignment assignment);
    Task<bool> UpdateAsync(CoordinatorAssignment assignment);
    Task<bool> DeleteAsync(int id);
}

public interface ICoordinationVisitRepository
{
    Task<IEnumerable<CoordinationVisit>> GetAllAsync(int? assignmentId = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<CoordinationVisit?> GetByIdAsync(int id);
    Task<int> AddAsync(CoordinationVisit visit);
    Task<bool> UpdateAsync(CoordinationVisit visit);
    Task<bool> DeleteAsync(int id);
    Task<int> BatchGenerateVisitsForMonthAsync(int academicYearId, int year, int month);
}

public interface IMonthlyPayrollRepository
{
    Task<IEnumerable<MonthlyPayroll>> GetAllAsync(int? academicYearId = null);
    Task<MonthlyPayroll?> GetByIdAsync(int id);
    Task<MonthlyPayroll?> GetByPeriodAsync(int academicYearId, int year, int month);
    Task<int> CreatePayrollAsync(int academicYearId, int year, int month);
    Task<bool> UpdateStatusAsync(int id, Enums.PayrollStatus status);
    Task<IEnumerable<PayrollItem>> GetPayrollItemsAsync(int payrollId);
    Task GeneratePayrollItemsAsync(int payrollId, decimal hourlyRate);
    Task<bool> DeletePayrollAsync(int id);
}

public interface IFeeReceiptRepository
{
    Task<IEnumerable<FeeReceipt>> GetForPeriodAsync(string period);
    Task<FeeReceipt?> GetByStudentAndPeriodAsync(int studentId, string period);
    Task<int> AddOrUpdateAsync(FeeReceipt receipt);
    Task<bool> DeleteAsync(int id);
    Task<bool> DeleteByStudentAndPeriodAsync(int studentId, string period);
}

public interface ICalendarTaskRepository
{
    Task<IEnumerable<CalendarTask>> GetAllAsync(bool includeInactive = false);
    Task<IEnumerable<CalendarTask>> GetTasksForMonthAsync(DateTime month);
    Task<CalendarTask?> GetByIdAsync(int id);
    Task<int> AddAsync(CalendarTask task);
    Task<bool> UpdateAsync(CalendarTask task);
    Task<bool> DeleteAsync(int id);
    Task ResetToDefaultsAsync();
}


