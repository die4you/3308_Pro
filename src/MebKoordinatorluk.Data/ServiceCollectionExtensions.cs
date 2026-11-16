using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Core.Services;
using MebKoordinatorluk.Data.Database;
using MebKoordinatorluk.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace MebKoordinatorluk.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMebDataServices(this IServiceCollection services, string? customDbPath = null)
    {
        // SQLite Config & Connection Factory
        services.AddSingleton(new DatabaseConfig(customDbPath));
        services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.AddTransient<DatabaseInitializer>();

        // Domain Services
        services.AddSingleton<ICoordinationCalculator, MebCoordinationCalculator>();
        services.AddSingleton<IDatabaseBackupService, Services.DatabaseBackupService>();
        services.AddSingleton<ILicenseService, Services.LicenseService>();
        services.AddScoped<ISgkCrossCheckService, Services.SgkCrossCheckService>();
        services.AddScoped<IAcademicYearRolloverService, Services.AcademicYearRolloverService>();

        // Repositories
        services.AddScoped<ISettingRepository, SettingRepository>();
        services.AddScoped<IFieldRepository, FieldRepository>();
        services.AddScoped<ISchoolClassRepository, SchoolClassRepository>();
        services.AddScoped<IAddressGroupRepository, AddressGroupRepository>();
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<IDocumentTrackingRepository, DocumentTrackingRepository>();

        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IAcademicYearRepository, AcademicYearRepository>();
        services.AddScoped<ITeacherRepository, TeacherRepository>();
        services.AddScoped<IEnterpriseRepository, EnterpriseRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<IStudentPlacementRepository, StudentPlacementRepository>();
        services.AddScoped<ICoordinatorAssignmentRepository, CoordinatorAssignmentRepository>();
        services.AddScoped<ICoordinationVisitRepository, CoordinationVisitRepository>();
        services.AddScoped<IMonthlyPayrollRepository, MonthlyPayrollRepository>();
        services.AddScoped<IFeeReceiptRepository, FeeReceiptRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICalendarTaskRepository, CalendarTaskRepository>();

        return services;
    }
}
