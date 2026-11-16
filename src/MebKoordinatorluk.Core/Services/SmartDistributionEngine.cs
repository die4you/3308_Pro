using MebKoordinatorluk.Core.Entities;

namespace MebKoordinatorluk.Core.Services;

public enum AssignmentStatus
{
    None,
    Valid,
    Warning,
    Conflict
}

public class AssignmentIssue
{
    public string Type { get; set; } = "warning"; // "warning" or "conflict"
    public string Text { get; set; } = string.Empty;
}

public class AssignmentAnalysisResult
{
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Valid;
    public List<AssignmentIssue> Issues { get; set; } = new();

    public string StatusText => Status switch
    {
        AssignmentStatus.Valid => "✓ Tam Uyumlu",
        AssignmentStatus.Warning => "⚠️ Uyarı",
        AssignmentStatus.Conflict => "❌ Gün Çakışması",
        _ => "Atanmamış"
    };
}

public class DistributionOptions
{
    public bool PrioritizeDayMatch { get; set; } = true;
    public bool PrioritizeBranchMatch { get; set; } = true;
    public bool ClusterEnterprises { get; set; } = true;
    public bool ClusterAddressGroups { get; set; } = true;
    public bool BalanceWorkload { get; set; } = true;
    public bool AssignUnassignedEnterprises { get; set; } = false;
}

public class DistributionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int NewlyAssignedEnterprises { get; set; }
    public int NewlyAssignedTeachers { get; set; }
    public int TotalStudents { get; set; }
    public int AssignedStudents { get; set; }
}

public class SmartDistributionEngine
{
    public static List<string> GetOverlappingDays(IEnumerable<string> days1, IEnumerable<string> days2)
    {
        var set1 = new HashSet<string>(days1.Select(NormalizeDay), StringComparer.OrdinalIgnoreCase);
        var set2 = new HashSet<string>(days2.Select(NormalizeDay), StringComparer.OrdinalIgnoreCase);
        return set1.Intersect(set2).ToList();
    }

    public static string NormalizeDay(string day)
    {
        if (string.IsNullOrWhiteSpace(day)) return "";
        return day.Trim().ToLowerInvariant()
                  .Replace("ı", "i")
                  .Replace("ğ", "g")
                  .Replace("ü", "u")
                  .Replace("ş", "s")
                  .Replace("ö", "o")
                  .Replace("ç", "c");
    }

    public static AssignmentAnalysisResult AnalyzeAssignment(Student student, Enterprise? enterprise, Teacher? teacher)
    {
        var result = new AssignmentAnalysisResult();
        if (student == null)
        {
            result.Status = AssignmentStatus.None;
            return result;
        }

        // 1. İşletme Kontrolü
        if (enterprise == null)
        {
            result.Issues.Add(new AssignmentIssue { Type = "warning", Text = "Öğrencinin henüz atanmış bir işletmesi yok." });
            result.Status = AssignmentStatus.Warning;
            return result;
        }

        // İşletme & Öğrenci Gün Uyumu
        var studentDays = student.GetDaysList();
        var enterpriseDays = enterprise.GetDaysList();
        if (studentDays.Count > 0 && enterpriseDays.Count > 0)
        {
            var commonDays = GetOverlappingDays(studentDays, enterpriseDays);
            if (commonDays.Count == 0)
            {
                result.Issues.Add(new AssignmentIssue
                {
                    Type = "warning",
                    Text = $"Öğrencinin staj günleri ({string.Join(", ", studentDays)}) ile işletmenin günleri ({string.Join(", ", enterpriseDays)}) uyuşmuyor!"
                });
                result.Status = AssignmentStatus.Warning;
            }
        }

        // 2. Öğretmen Kontrolü
        if (teacher == null)
        {
            result.Issues.Add(new AssignmentIssue { Type = "warning", Text = "Öğrenciye henüz bir koordinatör öğretmen atanmadı." });
            if (result.Status == AssignmentStatus.Valid) result.Status = AssignmentStatus.Warning;
            return result;
        }

        // Öğretmen Aktiflik Kontrolü
        if (!teacher.IsActive)
        {
            result.Issues.Add(new AssignmentIssue
            {
                Type = "conflict",
                Text = $"Öğretmen ({teacher.FullName}) sistemde pasif durumda!"
            });
            result.Status = AssignmentStatus.Conflict;
        }

        // Alan / Branş Uyumu
        var sField = student.FieldName?.Trim().ToLowerInvariant() ?? "";
        var tBranch = teacher.Branch?.Trim().ToLowerInvariant() ?? "";
        if (!string.IsNullOrWhiteSpace(sField) && !string.IsNullOrWhiteSpace(tBranch))
        {
            if (!sField.Contains(tBranch) && !tBranch.Contains(sField))
            {
                result.Issues.Add(new AssignmentIssue
                {
                    Type = "warning",
                    Text = $"Alan uyumsuzluğu: Öğrenci [{student.FieldName}], Öğretmen [{teacher.Branch}]."
                });
                if (result.Status != AssignmentStatus.Conflict) result.Status = AssignmentStatus.Warning;
            }
        }

        // Gün Uyumu (Öğretmenin koordinatörlük günü ile öğrencinin staj günleri)
        var teacherDays = teacher.GetDaysList();
        if (studentDays.Count > 0 && teacherDays.Count > 0)
        {
            var commonDays = GetOverlappingDays(studentDays, teacherDays);
            if (commonDays.Count == 0)
            {
                result.Issues.Add(new AssignmentIssue
                {
                    Type = "conflict",
                    Text = $"GÜN ÇAKIŞMASI: Öğretmenin koordinatörlük günleri ({string.Join(", ", teacherDays)}), öğrencinin staj günleri ({string.Join(", ", studentDays)}) ile KESİŞMİYOR!"
                });
                result.Status = AssignmentStatus.Conflict;
            }
        }

        return result;
    }

    public static DistributionResult RunAutoDistribution(
        DistributionOptions options,
        List<Teacher> teachers,
        List<Enterprise> enterprises,
        List<Student> students)
    {
        var activeTeachers = teachers.Where(t => t.IsActive).ToList();
        if (activeTeachers.Count == 0)
        {
            return new DistributionResult
            {
                Success = false,
                Message = "Aktif koordinatör öğretmen bulunamadı! Lütfen önce öğretmen ekleyin veya aktif yapın."
            };
        }

        if (students.Count == 0)
        {
            return new DistributionResult
            {
                Success = false,
                Message = "Sistemde kayıtlı öğrenci bulunamadı!"
            };
        }

        int newlyAssignedEnterprises = 0;
        int newlyAssignedTeachers = 0;

        // 1. İstenirse işletmesi olmayan öğrencileri işletmelere yerleştir
        if (options.AssignUnassignedEnterprises)
        {
            var capacities = enterprises.ToDictionary(e => e.Id, e => e.Capacity - students.Count(s => s.EnterpriseId == e.Id));

            foreach (var student in students.Where(s => !s.EnterpriseId.HasValue))
            {
                Enterprise? bestEnt = null;
                int bestScore = -999;

                foreach (var e in enterprises)
                {
                    if (capacities.TryGetValue(e.Id, out var remCap) && remCap <= 0) continue;

                    int score = 0;
                    // Alan uyumu
                    var sField = student.FieldName ?? "";
                    var eSector = e.Sector ?? "";
                    if (!string.IsNullOrWhiteSpace(sField) && !string.IsNullOrWhiteSpace(eSector) &&
                        (eSector.Contains(sField, StringComparison.OrdinalIgnoreCase) || sField.Contains(eSector, StringComparison.OrdinalIgnoreCase)))
                    {
                        score += 50;
                    }

                    // Gün uyumu
                    var common = GetOverlappingDays(student.GetDaysList(), e.GetDaysList());
                    score += common.Count * 20;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestEnt = e;
                    }
                }

                if (bestEnt != null)
                {
                    student.EnterpriseId = bestEnt.Id;
                    student.EnterpriseName = bestEnt.Name;
                    capacities[bestEnt.Id]--;
                    newlyAssignedEnterprises++;
                }
            }
        }

        // 2. İşletme ve Öğretmen Yük Takibi
        var teacherLoads = activeTeachers.ToDictionary(
            t => t.Id,
            t => new TeacherLoad { Teacher = t, StudentCount = 0, EnterpriseIds = new HashSet<int>() }
        );

        // İşletmelere göre öğrencileri grupla
        var enterpriseGroups = students.Where(s => s.EnterpriseId.HasValue)
                                       .GroupBy(s => s.EnterpriseId!.Value)
                                       .ToDictionary(g => g.Key, g => g.ToList());

        var noEnterpriseStudents = students.Where(s => !s.EnterpriseId.HasValue).ToList();

        // A) İŞLETME BÜTÜNLÜĞÜ (Her işletmeye öğrenci profiline en uygun öğretmeni ata)
        if (options.ClusterEnterprises)
        {
            foreach (var kv in enterpriseGroups)
            {
                var entId = kv.Key;
                var studentsInEnt = kv.Value;
                var ent = enterprises.FirstOrDefault(e => e.Id == entId);

                var fields = studentsInEnt.Select(s => s.FieldName).Where(f => !string.IsNullOrWhiteSpace(f)).ToList();
                var primaryField = fields.FirstOrDefault() ?? ent?.Sector ?? "";
                var allStudentDays = studentsInEnt.SelectMany(s => s.GetDaysList()).Distinct().ToList();

                Teacher? bestTeacher = null;
                int highestScore = -99999;

                foreach (var t in activeTeachers)
                {
                    int score = 100;
                    var load = teacherLoads[t.Id];

                    // 1. Gün Uyumu
                    var commonDays = GetOverlappingDays(t.GetDaysList(), allStudentDays);
                    if (commonDays.Count == 0)
                    {
                        score -= options.PrioritizeDayMatch ? 1000 : 200;
                    }
                    else
                    {
                        score += commonDays.Count * 40;
                    }

                    // 2. Branş Uyumu
                    if (options.PrioritizeBranchMatch && !string.IsNullOrWhiteSpace(primaryField) && !string.IsNullOrWhiteSpace(t.Branch))
                    {
                        if (primaryField.Contains(t.Branch, StringComparison.OrdinalIgnoreCase) ||
                            t.Branch.Contains(primaryField, StringComparison.OrdinalIgnoreCase))
                        {
                            score += 150;
                        }
                        else
                        {
                            score -= 80;
                        }
                    }

                    // 3. Yük Dengeleme
                    if (options.BalanceWorkload)
                    {
                        score -= (load.StudentCount * 10);
                        score -= (load.EnterpriseIds.Count * 15);
                    }

                    // 4. Adres / Bölge Grubu Yakınlığı
                    if (options.ClusterAddressGroups && ent != null && ent.AddressGroupId.HasValue)
                    {
                        bool hasSameGroup = load.EnterpriseIds.Any(otherId =>
                        {
                            var other = enterprises.FirstOrDefault(e => e.Id == otherId);
                            return other?.AddressGroupId == ent.AddressGroupId;
                        });

                        if (hasSameGroup) score += 100;
                    }

                    if (score > highestScore)
                    {
                        highestScore = score;
                        bestTeacher = t;
                    }
                }

                if (bestTeacher != null)
                {
                    if (ent != null)
                    {
                        ent.TeacherId = bestTeacher.Id;
                        ent.TeacherName = bestTeacher.FullName;
                    }

                    var tDays = bestTeacher.GetDaysList();
                    foreach (var s in studentsInEnt)
                    {
                        s.TeacherId = bestTeacher.Id;
                        s.TeacherName = bestTeacher.FullName;

                        // Öğrencinin işletme günleri ile öğretmenin gününün kesişimini görev günü yap
                        var sDays = s.GetDaysList();
                        var overlap = GetOverlappingDays(sDays, tDays);
                        s.DutyDays = overlap.Count > 0 ? string.Join(", ", overlap) : (tDays.FirstOrDefault() ?? "");

                        newlyAssignedTeachers++;
                    }

                    var load = teacherLoads[bestTeacher.Id];
                    load.StudentCount += studentsInEnt.Count;
                    load.EnterpriseIds.Add(entId);
                }
            }
        }

        // B) İşletmesi olmayan kalan öğrencilere bireysel öğretmen ataması
        foreach (var student in noEnterpriseStudents)
        {
            Teacher? bestTeacher = null;
            int highestScore = -99999;

            foreach (var t in activeTeachers)
            {
                int score = 50;
                var load = teacherLoads[t.Id];

                // Gün uyumu
                var common = GetOverlappingDays(student.GetDaysList(), t.GetDaysList());
                if (common.Count == 0) score -= 500;
                else score += common.Count * 30;

                // Branş uyumu
                if (!string.IsNullOrWhiteSpace(student.FieldName) && !string.IsNullOrWhiteSpace(t.Branch) &&
                    (student.FieldName.Contains(t.Branch, StringComparison.OrdinalIgnoreCase) || t.Branch.Contains(student.FieldName, StringComparison.OrdinalIgnoreCase)))
                {
                    score += 100;
                }

                if (options.BalanceWorkload)
                {
                    score -= (load.StudentCount * 10);
                }

                if (score > highestScore)
                {
                    highestScore = score;
                    bestTeacher = t;
                }
            }

            if (bestTeacher != null)
            {
                student.TeacherId = bestTeacher.Id;
                student.TeacherName = bestTeacher.FullName;
                student.DutyDays = bestTeacher.GetDaysList().FirstOrDefault() ?? "";
                newlyAssignedTeachers++;

                var load = teacherLoads[bestTeacher.Id];
                load.StudentCount += 1;
            }
        }

        int assignedCount = students.Count(s => s.TeacherId.HasValue && s.EnterpriseId.HasValue);

        return new DistributionResult
        {
            Success = true,
            Message = $"Akıllı dağıtım tamamlandı! {assignedCount} / {students.Count} öğrenci tam olarak eşleştirildi.",
            NewlyAssignedEnterprises = newlyAssignedEnterprises,
            NewlyAssignedTeachers = newlyAssignedTeachers,
            TotalStudents = students.Count,
            AssignedStudents = assignedCount
        };
    }

    private class TeacherLoad
    {
        public Teacher Teacher { get; set; } = null!;
        public int StudentCount { get; set; }
        public HashSet<int> EnterpriseIds { get; set; } = new();
    }
}
