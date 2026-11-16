using Dapper;
using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Interfaces;
using MebKoordinatorluk.Data.Database;

namespace MebKoordinatorluk.Data.Repositories;

public class StudentRepository : IStudentRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public StudentRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<Student>> GetAllAsync(int? departmentId = null, string? grade = null, bool includeInactive = false)
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
            SELECT s.*, 
                   COALESCE(s.FieldName, d.Name, '') AS FieldName,
                   COALESCE(s.Classroom, '') AS ClassName,
                   e.Name AS EnterpriseName,
                   t.FirstName || ' ' || t.LastName AS TeacherName
            FROM Students s
            LEFT JOIN Departments d ON s.DepartmentId = d.Id
            LEFT JOIN Enterprises e ON s.EnterpriseId = e.Id
            LEFT JOIN Teachers t ON s.TeacherId = t.Id
            WHERE (@IncludeInactive = 1 OR s.IsActive = 1)
              AND (@DeptId IS NULL OR s.DepartmentId = @DeptId OR s.FieldId = @DeptId)
              AND (@Grade IS NULL OR s.Grade = @Grade)
            ORDER BY s.Classroom, CAST(s.StudentNumber AS INTEGER), s.StudentNumber;
        ";
        return await conn.QueryAsync<Student>(sql, new { DeptId = departmentId, Grade = grade, IncludeInactive = includeInactive ? 1 : 0 });
    }

    public async Task<IEnumerable<Student>> GetArchivedStudentsAsync()
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT s.*, 
                   COALESCE(s.FieldName, d.Name, '') AS FieldName,
                   COALESCE(s.Classroom, '') AS ClassName,
                   e.Name AS EnterpriseName,
                   t.FirstName || ' ' || t.LastName AS TeacherName
            FROM Students s
            LEFT JOIN Departments d ON s.DepartmentId = d.Id
            LEFT JOIN Enterprises e ON s.EnterpriseId = e.Id
            LEFT JOIN Teachers t ON s.TeacherId = t.Id
            WHERE s.IsActive = 0 OR s.StudentNumber LIKE '%-P' OR s.StudentNumber LIKE '%-p'
            ORDER BY s.Classroom, s.FirstName, s.LastName;
        ";
        return await conn.QueryAsync<Student>(sql);
    }

    public async Task<Student?> GetByIdAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"
            SELECT s.*, 
                   COALESCE(s.FieldName, d.Name, '') AS FieldName,
                   COALESCE(s.Classroom, '') AS ClassName,
                   e.Name AS EnterpriseName,
                   t.FirstName || ' ' || t.LastName AS TeacherName
            FROM Students s
            LEFT JOIN Departments d ON s.DepartmentId = d.Id
            LEFT JOIN Enterprises e ON s.EnterpriseId = e.Id
            LEFT JOIN Teachers t ON s.TeacherId = t.Id
            WHERE s.Id = @Id;
        ";
        return await conn.QueryFirstOrDefaultAsync<Student>(sql, new { Id = id });
    }

    private static async Task EnsureValidDepartmentIdAsync(System.Data.IDbConnection conn, Student student)
    {
        // 1. Check if the given DepartmentId exists in Departments
        if (student.DepartmentId > 0)
        {
            var exists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Departments WHERE Id = @Id;", new { Id = student.DepartmentId });
            if (exists > 0) return;
        }

        // 2. If not, check if a department with matching FieldName exists
        if (!string.IsNullOrWhiteSpace(student.FieldName))
        {
            var matchedId = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM Departments WHERE Name = @Name LIMIT 1;", new { Name = student.FieldName });
            if (matchedId.HasValue && matchedId.Value > 0)
            {
                student.DepartmentId = matchedId.Value;
                return;
            }

            // 3. Create corresponding row in Departments so both tables stay synced and FK constraint is satisfied!
            try
            {
                var newId = await conn.QuerySingleAsync<int>(
                    "INSERT INTO Departments (Code, Name, SubBranch, IsActive) VALUES (@Code, @Name, @Name, 1); SELECT last_insert_rowid();",
                    new { Code = "ALAN" + (student.FieldId ?? 1), Name = student.FieldName });
                if (newId > 0)
                {
                    student.DepartmentId = newId;
                    return;
                }
            }
            catch
            {
                // Fallback below
            }
        }

        // 4. Fallback to the first existing Department ID
        var firstId = await conn.QueryFirstOrDefaultAsync<int?>("SELECT Id FROM Departments ORDER BY Id LIMIT 1;");
        if (firstId.HasValue && firstId.Value > 0)
        {
            student.DepartmentId = firstId.Value;
            return;
        }

        // 5. If Departments table is completely empty, create the default department (Id = 1)
        try
        {
            await conn.ExecuteAsync("INSERT INTO Departments (Id, Code, Name, SubBranch, IsActive) VALUES (1, 'GENEL', 'Genel', 'Genel', 1);");
            student.DepartmentId = 1;
        }
        catch
        {
            student.DepartmentId = 1;
        }
    }

    public async Task<int> AddAsync(Student student)
    {
        using var conn = _factory.CreateConnection();
        await EnsureValidDepartmentIdAsync(conn, student);

        const string sql = @"
            INSERT INTO Students (TcKimlikNo, Iban, StudentNumber, FirstName, LastName, DepartmentId, ClassId, Classroom, Grade, FieldId, FieldName, EnterpriseId, EnterpriseName, TeacherId, TeacherName, InternshipDays, DutyDays, FeeCategory, EducationType, BirthDate, SgkEntryDate, SgkExitDate, SgkPrimKodu, Gender, HasFamilyHealthInsurance, EducationPlace, Phone, ParentPhone, Notes, IsActive, IsSurekliDevamsiz, IsgStatus, IsgCertificateDate, IsgCertificateNo, IsgSecurityNo, IsIsgDocumentPrinted)
            VALUES (@TcKimlikNo, @Iban, @StudentNumber, @FirstName, @LastName, @DepartmentId, @ClassId, @Classroom, @Grade, @FieldId, @FieldName, @EnterpriseId, @EnterpriseName, @TeacherId, @TeacherName, @InternshipDays, @DutyDays, @FeeCategory, @EducationType, @BirthDate, @SgkEntryDate, @SgkExitDate, @SgkPrimKodu, @Gender, @HasFamilyHealthInsurance, @EducationPlace, @Phone, @ParentPhone, @Notes, @IsActive, @IsSurekliDevamsiz, @IsgStatus, @IsgCertificateDate, @IsgCertificateNo, @IsgSecurityNo, @IsIsgDocumentPrinted);
            SELECT last_insert_rowid();";
        return await conn.ExecuteScalarAsync<int>(sql, student);
    }

    public async Task<bool> UpdateAsync(Student student)
    {
        using var conn = _factory.CreateConnection();
        await EnsureValidDepartmentIdAsync(conn, student);

        const string sql = @"
            UPDATE Students SET
                TcKimlikNo = @TcKimlikNo,
                Iban = @Iban,
                StudentNumber = @StudentNumber,
                FirstName = @FirstName,
                LastName = @LastName,
                DepartmentId = @DepartmentId,
                ClassId = @ClassId,
                Classroom = @Classroom,
                Grade = @Grade,
                FieldId = @FieldId,
                FieldName = @FieldName,
                EnterpriseId = @EnterpriseId,
                EnterpriseName = @EnterpriseName,
                TeacherId = @TeacherId,
                TeacherName = @TeacherName,
                InternshipDays = @InternshipDays,
                DutyDays = @DutyDays,
                FeeCategory = @FeeCategory,
                EducationType = @EducationType,
                BirthDate = @BirthDate,
                SgkEntryDate = @SgkEntryDate,
                SgkExitDate = @SgkExitDate,
                SgkPrimKodu = @SgkPrimKodu,
                Gender = @Gender,
                HasFamilyHealthInsurance = @HasFamilyHealthInsurance,
                EducationPlace = @EducationPlace,
                Phone = @Phone,
                ParentPhone = @ParentPhone,
                Notes = @Notes,
                IsActive = @IsActive,
                IsSurekliDevamsiz = @IsSurekliDevamsiz,
                IsgStatus = @IsgStatus,
                IsgCertificateDate = @IsgCertificateDate,
                IsgCertificateNo = @IsgCertificateNo,
                IsgSecurityNo = @IsgSecurityNo,
                IsIsgDocumentPrinted = @IsIsgDocumentPrinted
            WHERE Id = @Id;";
        var affected = await conn.ExecuteAsync(sql, student);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        const string sql = "UPDATE Students SET IsActive = 0 WHERE Id = @Id;";
        var affected = await conn.ExecuteAsync(sql, new { Id = id });
        return affected > 0;
    }

    public async Task<bool> DeletePermanentlyAsync(int id)
    {
        using var conn = _factory.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync("DELETE FROM AttendanceEntries WHERE StudentId = @Id;", new { Id = id }, tx);
            await conn.ExecuteAsync("DELETE FROM FeeReceipts WHERE StudentId = @Id;", new { Id = id }, tx);
            await conn.ExecuteAsync("DELETE FROM StudentPlacements WHERE StudentId = @Id;", new { Id = id }, tx);
            var affected = await conn.ExecuteAsync("DELETE FROM Students WHERE Id = @Id;", new { Id = id }, tx);
            tx.Commit();
            return affected > 0;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> DeleteAllAsync()
    {
        using var conn = _factory.CreateConnection();
        const string sql = "DELETE FROM Students;";
        var affected = await conn.ExecuteAsync(sql);
        return affected >= 0;
    }

    public async Task<bool> BulkAssignAsync(IEnumerable<int> studentIds, int? enterpriseId, int? teacherId, string? dutyDays)
    {
        using var conn = _factory.CreateConnection();
        using var tx = conn.BeginTransaction();

        string? entName = null;
        if (enterpriseId.HasValue)
        {
            entName = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT Name FROM Enterprises WHERE Id = @Id;", new { Id = enterpriseId.Value }, transaction: tx);
        }

        string? teacherName = null;
        if (teacherId.HasValue)
        {
            teacherName = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT FirstName || ' ' || LastName FROM Teachers WHERE Id = @Id;", new { Id = teacherId.Value }, transaction: tx);
        }

        foreach (var id in studentIds)
        {
            var sql = @"
                UPDATE Students SET
                    EnterpriseId = COALESCE(@EntId, EnterpriseId),
                    EnterpriseName = COALESCE(@EntName, EnterpriseName),
                    TeacherId = COALESCE(@TeacherId, TeacherId),
                    TeacherName = COALESCE(@TeacherName, TeacherName),
                    DutyDays = COALESCE(@DutyDays, DutyDays)
                WHERE Id = @Id;";
            await conn.ExecuteAsync(sql, new
            {
                EntId = enterpriseId,
                EntName = entName,
                TeacherId = teacherId,
                TeacherName = teacherName,
                DutyDays = dutyDays,
                Id = id
            }, transaction: tx);
        }

        tx.Commit();
        return true;
    }

    public async Task<bool> ClearAllAssignmentsAsync()
    {
        using var conn = _factory.CreateConnection();
        const string sql = "UPDATE Students SET EnterpriseId = NULL, EnterpriseName = NULL, TeacherId = NULL, TeacherName = NULL, DutyDays = '';";
        await conn.ExecuteAsync(sql);
        return true;
    }

    public async Task<int> SyncClassInfoToStudentsAsync(SchoolClass schoolClass, string? oldClassName = null)
    {
        return await SyncClassInfoAndSgkCodesToStudentsAsync(schoolClass, oldClassName, false, false);
    }

    public async Task<int> SyncClassInfoAndSgkCodesToStudentsAsync(SchoolClass schoolClass, string? oldClassName = null, bool updateSgkCodes = false, bool toMesem = false, bool updateField = false)
    {
        using var conn = _factory.CreateConnection();
        string sql = @"
            UPDATE Students SET
                ClassId = @ClassId,
                Classroom = @ClassName,
                InternshipDays = @InternshipDays,
                EducationType = CASE WHEN @EducationType IS NOT NULL AND @EducationType != '' THEN @EducationType ELSE EducationType END"
            + (updateField ? @",
                FieldId = @FieldId,
                FieldName = @FieldName" : "")
            + @"
            WHERE (ClassId = @ClassId 
                   OR LOWER(Classroom) = LOWER(@ClassName) 
                   OR (@OldClassName IS NOT NULL AND LOWER(Classroom) = LOWER(@OldClassName)));";

        var affected = await conn.ExecuteAsync(sql, new
        {
            ClassId = schoolClass.Id,
            ClassName = schoolClass.Name,
            InternshipDays = schoolClass.InternshipDays ?? string.Empty,
            EducationType = schoolClass.EducationType,
            FieldId = schoolClass.FieldId,
            FieldName = schoolClass.FieldName,
            OldClassName = oldClassName
        });

        if (updateSgkCodes)
        {
            const string getStudentsSql = @"
                SELECT * FROM Students
                WHERE (ClassId = @ClassId 
                       OR LOWER(Classroom) = LOWER(@ClassName) 
                       OR (@OldClassName IS NOT NULL AND LOWER(Classroom) = LOWER(@OldClassName)))
                  AND IsActive = 1;";

            var classStudents = await conn.QueryAsync<Student>(getStudentsSql, new
            {
                ClassId = schoolClass.Id,
                ClassName = schoolClass.Name,
                OldClassName = oldClassName
            });

            foreach (var s in classStudents)
            {
                if (toMesem)
                {
                    s.EducationType = "MESEM";
                    bool isMale = string.Equals(s.Gender, "Erkek", StringComparison.OrdinalIgnoreCase);
                    bool isMaleOver20 = isMale && s.Age.HasValue && s.Age.Value >= 20;
                    s.SgkPrimKodu = (s.HasFamilyHealthInsurance && !isMaleOver20) ? "7" : "42";
                }
                else
                {
                    s.EducationType = "Örgün Eğitim";
                    s.SgkPrimKodu = s.HasFamilyHealthInsurance ? "49" : "50";
                }

                await UpdateAsync(s);
            }
        }

        return affected;
    }

    public async Task<bool> ClearBulkSgkDatesAsync(IEnumerable<int> studentIds, bool clearEntry, bool clearExit)
    {
        var idList = studentIds?.ToList();
        if (idList == null || idList.Count == 0 || (!clearEntry && !clearExit)) return false;

        using var conn = _factory.CreateConnection();
        var sql = $@"
            UPDATE Students SET
                SgkEntryDate = {(clearEntry ? "NULL" : "SgkEntryDate")},
                SgkExitDate = {(clearExit ? "NULL" : "SgkExitDate")}
            WHERE Id IN @Ids;";

        var affected = await conn.ExecuteAsync(sql, new { Ids = idList });
        return affected > 0;
    }
}
