using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace MebKoordinatorluk.Data.Database;

public class DatabaseInitializer
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public DatabaseInitializer(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Initialize()
    {
        using var connection = (SqliteConnection)_connectionFactory.CreateConnection();
        Initialize(connection);
    }

    public void Initialize(SqliteConnection connection)
    {
        // 1. Performans ve Güvenlik PRAGMA Komutları
        connection.Execute("PRAGMA journal_mode = WAL;");
        connection.Execute("PRAGMA foreign_keys = ON;");
        connection.Execute("PRAGMA busy_timeout = 5000;");

        // 2. Tablo Şemalarını Oluştur
        CreateTables(connection);

        // 3. Mevcut Tablolara Eksik Sütunları Ekle (Migrate Columns)
        MigrateColumns(connection);

        // 4. İndeksleri Oluştur
        CreateIndexes(connection);

        // 5. Varsayılan Başlangıç Verilerini (Seed Data) Yükle
        SeedDefaultData(connection);
    }

    private void CreateTables(SqliteConnection connection)
    {
        const string sql = @"
            -- Sistem Parametreleri ve Ayarları
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                Description TEXT,
                UpdatedDate TEXT NOT NULL
            );

            -- Yıl Bazlı Asgari Ücret Tanımları
            CREATE TABLE IF NOT EXISTS YearlyMinimumWages (
                Year INTEGER PRIMARY KEY,
                NetMinimumWage REAL NOT NULL,
                GrossMinimumWage REAL NOT NULL,
                UpdatedDate TEXT
            );

            -- Mesleki Alanlar ve Dallar
            CREATE TABLE IF NOT EXISTS Fields (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT,
                Branches TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            -- Sınıf ve Şubeler
            CREATE TABLE IF NOT EXISTS SchoolClasses (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                FieldId INTEGER,
                FieldName TEXT,
                InstitutionType TEXT NOT NULL DEFAULT 'Anadolu Meslek Programı',
                EducationType TEXT NOT NULL DEFAULT 'Örgün Eğitim',
                InternshipDays TEXT NOT NULL DEFAULT 'Pazartesi,Salı,Çarşamba',
                StartDate TEXT,
                EndDate TEXT,
                IgnoreHalfDayDeduction INTEGER NOT NULL DEFAULT 0,
                HasInternship INTEGER NOT NULL DEFAULT 1,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            -- Adres ve Bölge Grupları
            CREATE TABLE IF NOT EXISTS AddressGroups (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Region TEXT,
                Description TEXT,
                Color TEXT DEFAULT 'blue',
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            -- Koordinatör Öğretmenler
            CREATE TABLE IF NOT EXISTS Teachers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TcKimlikNo TEXT UNIQUE,
                FirstName TEXT NOT NULL,
                LastName TEXT NOT NULL,
                DepartmentId INTEGER NOT NULL DEFAULT 1,
                Branch TEXT,
                Email TEXT,
                Phone TEXT,
                AvailableDays TEXT DEFAULT 'Salı',
                EmploymentType INTEGER NOT NULL DEFAULT 1,
                MaxCoordinationHours INTEGER NOT NULL DEFAULT 16,
                TargetHours INTEGER NOT NULL DEFAULT 16,
                CoordinatorFeeHours INTEGER NOT NULL DEFAULT 16,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            -- Staj İşletmeleri
            CREATE TABLE IF NOT EXISTS Enterprises (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                AddressGroupId INTEGER,
                AddressGroupName TEXT,
                Sector TEXT,
                ContactPerson TEXT,
                Phone TEXT,
                Email TEXT,
                Address TEXT,
                District TEXT,
                City TEXT DEFAULT 'İSTANBUL',
                DistanceType INTEGER NOT NULL DEFAULT 1,
                DistanceText TEXT DEFAULT 'Mücavir Alan İçi',
                Capacity INTEGER NOT NULL DEFAULT 4,
                ActiveDays TEXT DEFAULT 'Pazartesi,Salı,Çarşamba',
                TeacherId INTEGER,
                TeacherName TEXT,
                TaxNumber TEXT,
                Iban TEXT,
                SgkSicilNo TEXT,
                MasterTrainerName TEXT,
                Notes TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            -- Eğitim Öğretim Yılları
            CREATE TABLE IF NOT EXISTS AcademicYears (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Term INTEGER NOT NULL DEFAULT 1,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            -- Alanlar ve Dallar (Eski uyumluluk)
            CREATE TABLE IF NOT EXISTS Departments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Code TEXT NOT NULL,
                Name TEXT NOT NULL,
                SubBranch TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            -- Stajyer Öğrenciler
            CREATE TABLE IF NOT EXISTS Students (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TcKimlikNo TEXT,
                Iban TEXT,
                StudentNumber TEXT NOT NULL,
                FirstName TEXT NOT NULL,
                LastName TEXT NOT NULL,
                DepartmentId INTEGER REFERENCES Departments(Id) DEFAULT 1,
                ClassId INTEGER,
                Classroom TEXT NOT NULL DEFAULT '12-A',
                Grade TEXT NOT NULL DEFAULT '12',
                FieldId INTEGER,
                FieldName TEXT,
                EnterpriseId INTEGER,
                EnterpriseName TEXT,
                TeacherId INTEGER,
                TeacherName TEXT,
                InternshipDays TEXT DEFAULT 'Pazartesi,Salı,Çarşamba',
                DutyDays TEXT DEFAULT 'Salı',
                FeeCategory TEXT DEFAULT 'kat1',
                EducationType TEXT DEFAULT 'Örgün Eğitim',
                BirthDate TEXT,
                SgkEntryDate TEXT,
                SgkExitDate TEXT,
                SgkPrimKodu TEXT,
                Gender TEXT DEFAULT 'Erkek',
                HasFamilyHealthInsurance INTEGER DEFAULT 1,
                EducationPlace TEXT DEFAULT 'İşletmede (Staj)',
                Phone TEXT,
                ParentName TEXT,
                ParentPhone TEXT,
                Notes TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            -- Aylık Devam Devamsızlık Çizelgesi (3308 Sayılı Kanun)
            CREATE TABLE IF NOT EXISTS AttendanceEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Year INTEGER NOT NULL,
                Month INTEGER NOT NULL,
                StudentId INTEGER NOT NULL,
                DayOfMonth INTEGER NOT NULL,
                Code TEXT NOT NULL DEFAULT 'X'
            );

            -- Koordinatörlük Evrak Teslim ve Takip Kayıtları
            CREATE TABLE IF NOT EXISTS DocumentTrackingRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Year INTEGER NOT NULL,
                Month INTEGER NOT NULL,
                TeacherId INTEGER NOT NULL,
                EnterpriseId INTEGER NOT NULL,
                TransitionStart INTEGER NOT NULL DEFAULT 0,
                Week1 INTEGER NOT NULL DEFAULT 0,
                Week2 INTEGER NOT NULL DEFAULT 0,
                Week3 INTEGER NOT NULL DEFAULT 0,
                Week4 INTEGER NOT NULL DEFAULT 0,
                TransitionEnd INTEGER NOT NULL DEFAULT 0,
                MonthlyReport INTEGER NOT NULL DEFAULT 0,
                AttendanceSheet INTEGER NOT NULL DEFAULT 0,
                Notes TEXT
            );

            -- Öğrenci - İşletme Staj Eşleşmeleri (Eski uyumluluk)
            CREATE TABLE IF NOT EXISTS StudentPlacements (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AcademicYearId INTEGER NOT NULL,
                StudentId INTEGER NOT NULL,
                EnterpriseId INTEGER NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL,
                InternshipDays TEXT,
                ContractNo TEXT,
                Status INTEGER NOT NULL DEFAULT 1
            );

            -- Öğretmen Koordinatörlük Dağıtımları (Eski uyumluluk)
            CREATE TABLE IF NOT EXISTS CoordinatorAssignments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AcademicYearId INTEGER NOT NULL,
                TeacherId INTEGER NOT NULL,
                EnterpriseId INTEGER NOT NULL,
                DayOfWeek INTEGER NOT NULL DEFAULT 1,
                StudentCount INTEGER NOT NULL DEFAULT 0,
                CalculatedHours REAL NOT NULL DEFAULT 0,
                Notes TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            -- Koordinatörlük İşletme Ziyaret Kayıtları (Eski uyumluluk)
            CREATE TABLE IF NOT EXISTS CoordinationVisits (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AssignmentId INTEGER NOT NULL,
                VisitDate TEXT NOT NULL,
                Status INTEGER NOT NULL DEFAULT 1,
                FormNumber TEXT,
                ObservationNotes TEXT,
                CreatedAt TEXT NOT NULL
            );

            -- Aylık Puantaj Bordroları (Eski uyumluluk)
            CREATE TABLE IF NOT EXISTS MonthlyPayrolls (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AcademicYearId INTEGER NOT NULL,
                Year INTEGER NOT NULL,
                Month INTEGER NOT NULL,
                Status INTEGER NOT NULL DEFAULT 1,
                CreatedDate TEXT NOT NULL,
                ApprovedDate TEXT
            );

            -- Aylık Puantaj Kalemleri (Eski uyumluluk)
            CREATE TABLE IF NOT EXISTS PayrollItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PayrollId INTEGER NOT NULL,
                TeacherId INTEGER NOT NULL,
                Week1Hours REAL NOT NULL DEFAULT 0,
                Week2Hours REAL NOT NULL DEFAULT 0,
                Week3Hours REAL NOT NULL DEFAULT 0,
                Week4Hours REAL NOT NULL DEFAULT 0,
                Week5Hours REAL NOT NULL DEFAULT 0,
                TotalHours REAL NOT NULL DEFAULT 0,
                HourlyRate REAL NOT NULL DEFAULT 0,
                TotalAmount REAL NOT NULL DEFAULT 0,
                Notes TEXT
            );

            -- Öğrenci Dekont Arşiv Takip Kayıtları
            CREATE TABLE IF NOT EXISTS FeeReceipts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StudentId INTEGER NOT NULL,
                Period TEXT NOT NULL,
                FileName TEXT NOT NULL,
                ArchivePath TEXT NOT NULL,
                AuditDate TEXT NOT NULL,
                MatchReason TEXT
            );

            -- Kullanıcılar ve Yetki Yönetimi
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                PasswordSalt TEXT NOT NULL,
                FullName TEXT NOT NULL,
                Role TEXT NOT NULL DEFAULT 'Admin',
                TeacherId INTEGER NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                LastLoginAt TEXT NULL
            );

            -- Lisans Bilgileri
            CREATE TABLE IF NOT EXISTS Licenses (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MachineCode TEXT NOT NULL,
                SchoolName TEXT,
                CityDistrict TEXT,
                StartDate TEXT,
                EndDate TEXT,
                LicenseKey TEXT,
                ActivatedAt TEXT,
                LastVerifiedAt TEXT NOT NULL,
                Status INTEGER NOT NULL DEFAULT 2,
                Note TEXT
            );

            -- Lisans Aktivasyon Geçmişi
            CREATE TABLE IF NOT EXISTS LicenseHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MachineCode TEXT NOT NULL,
                SchoolName TEXT,
                LicenseKey TEXT NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL,
                ActivatedAt TEXT NOT NULL,
                Status TEXT NOT NULL,
                Note TEXT
            );

            -- İdari İşlem ve Görev Takvimi
            CREATE TABLE IF NOT EXISTS CalendarTasks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Subtitle TEXT,
                StartDay INTEGER NOT NULL DEFAULT 1,
                EndDay INTEGER NOT NULL DEFAULT 5,
                RangeText TEXT,
                FrequencyType INTEGER NOT NULL DEFAULT 0,
                TargetMonth INTEGER,
                TargetYearMonth TEXT,
                NavTarget TEXT,
                NavBtnText TEXT,
                IsSystemDefault INTEGER NOT NULL DEFAULT 0,
                IsActive INTEGER NOT NULL DEFAULT 1,
                OrderIndex INTEGER NOT NULL DEFAULT 0
            );
        ";

        connection.Execute(sql);
    }

    private void MigrateColumns(SqliteConnection connection)
    {
        // Teachers tablosuna yeni sütunlar
        AddColumnIfNotExists(connection, "Teachers", "Branch", "TEXT");
        AddColumnIfNotExists(connection, "Teachers", "AvailableDays", "TEXT DEFAULT 'Salı'");
        AddColumnIfNotExists(connection, "Teachers", "TargetHours", "INTEGER DEFAULT 16");
        AddColumnIfNotExists(connection, "Teachers", "CoordinatorFeeHours", "INTEGER DEFAULT 16");

        // SchoolClasses tablosuna yeni sütunlar
        AddColumnIfNotExists(connection, "SchoolClasses", "IgnoreHalfDayDeduction", "INTEGER DEFAULT 0");
        AddColumnIfNotExists(connection, "SchoolClasses", "HasInternship", "INTEGER DEFAULT 1");

        // Enterprises tablosuna yeni sütunlar
        AddColumnIfNotExists(connection, "Enterprises", "AddressGroupId", "INTEGER");
        AddColumnIfNotExists(connection, "Enterprises", "AddressGroupName", "TEXT");
        AddColumnIfNotExists(connection, "Enterprises", "Sector", "TEXT");
        AddColumnIfNotExists(connection, "Enterprises", "DistanceText", "TEXT DEFAULT 'Mücavir Alan İçi'");
        AddColumnIfNotExists(connection, "Enterprises", "Capacity", "INTEGER DEFAULT 4");
        AddColumnIfNotExists(connection, "Enterprises", "ActiveDays", "TEXT DEFAULT 'Pazartesi,Salı,Çarşamba'");
        AddColumnIfNotExists(connection, "Enterprises", "TeacherId", "INTEGER");
        AddColumnIfNotExists(connection, "Enterprises", "TeacherName", "TEXT");
        AddColumnIfNotExists(connection, "Enterprises", "Iban", "TEXT");
        AddColumnIfNotExists(connection, "Enterprises", "SgkSicilNo", "TEXT");
        AddColumnIfNotExists(connection, "Enterprises", "OptOutStateContribution", "INTEGER DEFAULT 0");
        AddColumnIfNotExists(connection, "Enterprises", "EmployeeCount", "INTEGER DEFAULT 1");
        AddColumnIfNotExists(connection, "Enterprises", "Notes", "TEXT");

        // Students tablosuna yeni sütunlar
        AddColumnIfNotExists(connection, "Students", "Iban", "TEXT");
        AddColumnIfNotExists(connection, "Students", "ClassId", "INTEGER");
        AddColumnIfNotExists(connection, "Students", "Classroom", "TEXT DEFAULT '12-A'");
        AddColumnIfNotExists(connection, "Students", "FieldId", "INTEGER");
        AddColumnIfNotExists(connection, "Students", "FieldName", "TEXT");
        AddColumnIfNotExists(connection, "Students", "EnterpriseId", "INTEGER");
        AddColumnIfNotExists(connection, "Students", "EnterpriseName", "TEXT");
        AddColumnIfNotExists(connection, "Students", "TeacherId", "INTEGER");
        AddColumnIfNotExists(connection, "Students", "TeacherName", "TEXT");
        AddColumnIfNotExists(connection, "Students", "InternshipDays", "TEXT DEFAULT 'Pazartesi,Salı,Çarşamba'");
        AddColumnIfNotExists(connection, "Students", "DutyDays", "TEXT DEFAULT 'Salı'");
        AddColumnIfNotExists(connection, "Students", "FeeCategory", "TEXT DEFAULT 'kat1'");
        AddColumnIfNotExists(connection, "Students", "EducationType", "TEXT DEFAULT 'Örgün Eğitim'");
        AddColumnIfNotExists(connection, "Students", "BirthDate", "TEXT");
        AddColumnIfNotExists(connection, "Students", "SgkEntryDate", "TEXT");
        AddColumnIfNotExists(connection, "Students", "SgkExitDate", "TEXT");
        AddColumnIfNotExists(connection, "Students", "SgkPrimKodu", "TEXT");
        AddColumnIfNotExists(connection, "Students", "Gender", "TEXT DEFAULT 'Erkek'");
        AddColumnIfNotExists(connection, "Students", "HasFamilyHealthInsurance", "INTEGER DEFAULT 1");
        AddColumnIfNotExists(connection, "Students", "EducationPlace", "TEXT DEFAULT 'İşletmede (Staj)'");
        AddColumnIfNotExists(connection, "Students", "Phone", "TEXT");
        AddColumnIfNotExists(connection, "Students", "ParentName", "TEXT");
        AddColumnIfNotExists(connection, "Students", "ParentPhone", "TEXT");
        AddColumnIfNotExists(connection, "Students", "Notes", "TEXT");
        AddColumnIfNotExists(connection, "Students", "IsSurekliDevamsiz", "INTEGER DEFAULT 0");
        AddColumnIfNotExists(connection, "Students", "IsgStatus", "TEXT DEFAULT 'Sertifikasız (Eksik)'");
        AddColumnIfNotExists(connection, "Students", "IsgCertificateDate", "TEXT");
        AddColumnIfNotExists(connection, "Students", "IsgCertificateNo", "TEXT");
        AddColumnIfNotExists(connection, "Students", "IsgSecurityNo", "TEXT");
        AddColumnIfNotExists(connection, "Students", "IsIsgDocumentPrinted", "INTEGER DEFAULT 0");

        // DocumentTrackingRecords tablosuna geçiş sütunları
        AddColumnIfNotExists(connection, "DocumentTrackingRecords", "TransitionStart", "INTEGER DEFAULT 0");
        AddColumnIfNotExists(connection, "DocumentTrackingRecords", "TransitionEnd", "INTEGER DEFAULT 0");

        try
        {
            connection.Execute(@"
                UPDATE Students SET Grade = '9' WHERE Classroom LIKE '%9-%' OR Classroom LIKE '9%';
                UPDATE Students SET Grade = '10' WHERE Classroom LIKE '%10-%' OR Classroom LIKE '10%';
                UPDATE Students SET Grade = '11' WHERE Classroom LIKE '%11-%' OR Classroom LIKE '11%';
                UPDATE Students SET Grade = '12' WHERE Classroom LIKE '%12-%' OR Classroom LIKE '12%';
                UPDATE Students SET EducationPlace = 'Okulda' WHERE (EducationType LIKE '%Anadolu%' OR EducationType LIKE '%Örgün%' OR EducationType IS NULL OR EducationType = '') AND (Grade IN ('9', '10', '11') OR Classroom LIKE '9%' OR Classroom LIKE '10%' OR Classroom LIKE '11%');
                UPDATE Teachers SET TcKimlikNo = NULL WHERE TcKimlikNo = '' OR TRIM(TcKimlikNo) = '';
            ");
        }
        catch { }
    }

    private void AddColumnIfNotExists(SqliteConnection connection, string table, string column, string colType)
    {
        try
        {
            var cols = connection.Query<string>($"PRAGMA table_info({table});").ToList();
            var colNames = connection.Query($"PRAGMA table_info({table});").Select(r => (string)r.name).ToList();
            if (!colNames.Contains(column, StringComparer.OrdinalIgnoreCase))
            {
                connection.Execute($"ALTER TABLE {table} ADD COLUMN {column} {colType};");
            }
        }
        catch
        {
            // Column already exists or table issue
        }
    }

    private void CreateIndexes(SqliteConnection connection)
    {
        const string sql = @"
            CREATE INDEX IF NOT EXISTS IX_Attendance_Period ON AttendanceEntries(Year, Month, StudentId);
            CREATE INDEX IF NOT EXISTS IX_DocTracking_Period ON DocumentTrackingRecords(Year, Month, TeacherId, EnterpriseId);
            CREATE INDEX IF NOT EXISTS IX_Students_ClassId ON Students(ClassId);
            CREATE INDEX IF NOT EXISTS IX_Students_EnterpriseId ON Students(EnterpriseId);
            CREATE INDEX IF NOT EXISTS IX_Students_TeacherId ON Students(TeacherId);
            CREATE INDEX IF NOT EXISTS IX_Enterprises_AddressGroupId ON Enterprises(AddressGroupId);
            CREATE UNIQUE INDEX IF NOT EXISTS IX_FeeReceipts_Student_Period ON FeeReceipts(StudentId, Period);
        ";

        connection.Execute(sql);
    }

    private void SeedDefaultData(SqliteConnection connection)
    {
        var now = DateTime.UtcNow.ToString("o");

        // 1. Ayarlar (Settings)
        var settingCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Settings;");
        if (settingCount == 0)
        {
            var curYear = DateTime.Now.Year;
            var defaultSettings = new[]
            {
                new { Key = "Valilik", Value = "", Description = "İl Valiliği / Kaymakamlık", UpdatedDate = now },
                new { Key = "DistrictMem", Value = "", Description = "İl / İlçe Millî Eğitim Müdürlüğü", UpdatedDate = now },
                new { Key = "KurumKodu", Value = "", Description = "Kurum Kodu (MEBBİS)", UpdatedDate = now },
                new { Key = "SchoolName", Value = "", Description = "Okul / Kurum Resmi Adı", UpdatedDate = now },
                new { Key = "SayiNo", Value = "", Description = "Resmi Yazı Sayı No", UpdatedDate = now },
                new { Key = "FormDate", Value = DateTime.Now.ToString("dd/MM/yyyy"), Description = "Yazı Tarihi", UpdatedDate = now },
                new { Key = "ValidityDate", Value = DateTime.Now.ToString("dd/MM/yyyy"), Description = "Geçerlilik Tarihi", UpdatedDate = now },
                new { Key = "TeacherTitle", Value = "Öğretmen", Description = "Öğretmen Hitap Unvanı", UpdatedDate = now },
                new { Key = "AcademicYear", Value = $"{curYear}-{curYear + 1}", Description = "Eğitim Öğretim Yılı", UpdatedDate = now },
                new { Key = "Term", Value = "1.", Description = "Dönem", UpdatedDate = now },
                new { Key = "FormBodyText", Value = "{yil} öğretim yılı {donem} Dönem'inde 3308 sayılı Mesleki Eğitim Kanunu ve Millî Eğitim Bakanlığı Ortaöğretim Kurumları Yönetmeliği'nin 88 inci maddesi gereğince, {tarih} tarihinden geçerli olmak üzere aşağıda bilgileri verilen işletmelerin koordinatörlük görevleri tarafınıza verilmiştir.", Description = "Görevlendirme Paragrafı", UpdatedDate = now },
                new { Key = "PrincipalName", Value = "", Description = "Okul Müdürü", UpdatedDate = now },
                new { Key = "VicePrincipalName", Value = "", Description = "Koordinatör Müdür Yardımcısı", UpdatedDate = now },
                new { Key = "IncludeSaturday", Value = "false", Description = "Cumartesi Dahil", UpdatedDate = now },
                new { Key = "MaxHoursPerTeacher", Value = "16", Description = "Haftalık Azami Saat", UpdatedDate = now },
                new { Key = "MinimumWage", Value = "28075.50", Description = "Net Asgari Ücret (TL)", UpdatedDate = now },
                new { Key = "MinimumWageYear", Value = curYear.ToString(), Description = "Asgari Ücret Bütçe Yılı", UpdatedDate = now },
                new { Key = "OrgunStartDate", Value = $"{curYear}-09-09", Description = "Örgün Eğitim Başlangıç Tarihi", UpdatedDate = now },
                new { Key = "OrgunEndDate", Value = $"{curYear + 1}-06-20", Description = "Örgün Eğitim Bitiş Tarihi", UpdatedDate = now },
                new { Key = "Username", Value = "admin", Description = "Yönetici Kullanıcı Adı", UpdatedDate = now },
                new { Key = "Password", Value = "admin123", Description = "Yönetici Şifresi", UpdatedDate = now }
            };

            const string insertSettingSql = @"
                INSERT INTO Settings (Key, Value, Description, UpdatedDate)
                VALUES (@Key, @Value, @Description, @UpdatedDate);";

            connection.Execute(insertSettingSql, defaultSettings);
        }

        // 2. Yıl Bazlı Asgari Ücretler (YearlyMinimumWages)
        var yearlyWageCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM YearlyMinimumWages;");
        if (yearlyWageCount == 0)
        {
            var curYear = DateTime.Now.Year;
            decimal netCurrent = 28075.50m;
            decimal grossCurrent = Math.Round(netCurrent / 0.85m, 2);

            const string insertYearlySql = @"
                INSERT INTO YearlyMinimumWages (Year, NetMinimumWage, GrossMinimumWage, UpdatedDate)
                VALUES (@Year, @NetMinimumWage, @GrossMinimumWage, @UpdatedDate);";

            connection.Execute(insertYearlySql, new[]
            {
                new { Year = 2025, NetMinimumWage = 22104.00m, GrossMinimumWage = 26004.71m, UpdatedDate = now },
                new { Year = 2026, NetMinimumWage = netCurrent, GrossMinimumWage = grossCurrent, UpdatedDate = now }
            });
        }

        // 3. Alan / Dal Havuzu (Fields) - Standart MEB Alanları
        var fieldCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Fields;");
        if (fieldCount == 0)
        {
            const string insertFieldsSql = @"
                INSERT INTO Fields (Name, Description, Branches, IsActive) VALUES
                ('Bilişim Teknolojileri', 'Yazılım Geliştirme, Ağ İşletmenliği ve Siber Güvenlik, Veritabanı Programcılığı, Web Programcılığı', 'Yazılım Geliştirme, Ağ İşletmenliği ve Siber Güvenlik, Veritabanı Programcılığı, Web Programcılığı', 1),
                ('Elektrik-Elektronik Teknolojisi', 'Endüstriyel Bakım Onarım, Elektrik Tesisatları ve Dağıtımı, Pano Montörlüğü, Haberleşme Sistemleri', 'Endüstriyel Bakım Onarım, Elektrik Tesisatları ve Dağıtımı, Pano Montörlüğü, Haberleşme Sistemleri', 1),
                ('Muhasebe ve Finansman', 'Bilgisayarlı Muhasebe, Dış Ticaret Ofis Hizmetleri, Finans ve Borsa Hizmetleri', 'Bilgisayarlı Muhasebe, Dış Ticaret Ofis Hizmetleri, Finans ve Borsa Hizmetleri', 1),
                ('Makine ve Tasarım Teknolojisi', 'Bilgisayarlı Makine İmalatı (CNC), Endüstriyel Kalıp, Makine Bakım Onarım, Bilgisayar Destekli Makine Ressamlığı', 'Bilgisayarlı Makine İmalatı (CNC), Endüstriyel Kalıp, Makine Bakım Onarım', 1),
                ('Motorlu Araçlar Teknolojisi', 'Otomotiv Elektromekanik, Otomotiv Gövde, Otomotiv Boya', 'Otomotiv Elektromekanik, Otomotiv Gövde, Otomotiv Boya', 1);
            ";
            connection.Execute(insertFieldsSql);
        }

        // 4. Eğitim Öğretim Yılları (AcademicYears)
        var ayCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM AcademicYears;");
        if (ayCount == 0)
        {
            var curY = DateTime.Now.Year;
            connection.Execute(@"
                INSERT INTO AcademicYears (Name, Term, StartDate, EndDate, IsActive)
                VALUES (@Name, 1, @StartDate, @EndDate, 1);",
                new { Name = $"{curY}-{curY + 1}", StartDate = $"{curY}-09-09", EndDate = $"{curY + 1}-06-20" });
        }

        // 5. Alanlar ve Dallar (Departments) - Standart MEB Dallar
        var depCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Departments;");
        if (depCount == 0)
        {
            connection.Execute(@"
                INSERT INTO Departments (Code, Name, SubBranch, IsActive) VALUES
                ('BIL01', 'Bilişim Teknolojileri', 'Yazılım Geliştirme', 1),
                ('BIL02', 'Bilişim Teknolojileri', 'Ağ İşletmenliği ve Siber Güvenlik', 1),
                ('ELK01', 'Elektrik-Elektronik Teknolojisi', 'Endüstriyel Bakım Onarım', 1),
                ('ELK02', 'Elektrik-Elektronik Teknolojisi', 'Elektrik Tesisatları ve Pano Montörlüğü', 1),
                ('MUH01', 'Muhasebe ve Finansman', 'Bilgisayarlı Muhasebe', 1),
                ('MAK01', 'Makine ve Tasarım Teknolojisi', 'Bilgisayarlı Makine İmalatı (CNC)', 1);");
        }

        // NOT: Öğretmenler (Teachers), İşletmeler (Enterprises), Öğrenciler (Students), Sınıflar (SchoolClasses)
        // ve Adres Grupları (AddressGroups) dağıtım paketinde 0 kayıt olarak temiz başlatılır.
        // Her okul kendi verilerini tanımlar veya e-Okul / Excel'den içe aktarır.

        // 6. Kullanıcılar (Users)
        var userCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Users;");
        if (userCount == 0)
        {
            var existingUsername = connection.QueryFirstOrDefault<string>("SELECT Value FROM Settings WHERE Key = 'Username';");
            var existingPassword = connection.QueryFirstOrDefault<string>("SELECT Value FROM Settings WHERE Key = 'Password';");

            string username = !string.IsNullOrWhiteSpace(existingUsername) ? existingUsername.Trim() : "admin";
            string password = !string.IsNullOrWhiteSpace(existingPassword) ? existingPassword.Trim() : "admin";

            var (hash, salt) = MebKoordinatorluk.Core.Security.PasswordSecurity.HashPassword(password);

            connection.Execute(@"
                INSERT INTO Users (Username, PasswordHash, PasswordSalt, FullName, Role, IsActive, CreatedAt)
                VALUES (@Username, @PasswordHash, @PasswordSalt, @FullName, 'Admin', 1, @CreatedAt);",
                new
                {
                    Username = username,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    FullName = "Sistem Yöneticisi",
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
        }

        // 7. İdari Görev Takvimi Standart Görevleri (CalendarTasks)
        var taskCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM CalendarTasks;");
        if (taskCount == 0)
        {
            var defaultTasks = Repositories.CalendarTaskRepository.GetDefaultTasks();
            foreach (var task in defaultTasks)
            {
                connection.Execute(@"
                    INSERT INTO CalendarTasks 
                    (Title, Subtitle, StartDay, EndDay, RangeText, FrequencyType, TargetMonth, TargetYearMonth, NavTarget, NavBtnText, IsSystemDefault, IsActive, OrderIndex)
                    VALUES 
                    (@Title, @Subtitle, @StartDay, @EndDay, @RangeText, @FrequencyType, @TargetMonth, @TargetYearMonth, @NavTarget, @NavBtnText, @IsSystemDefault, @IsActive, @OrderIndex);",
                    task);
            }
        }
    }
}
