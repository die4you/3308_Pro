using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Services;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class FeeCalculatorTests
{
    [Fact]
    public void CalculateMonthlyFees_WithAbsenceAndBlankDays_ShouldDeductBothCorrectly()
    {
        // Arrange
        const decimal minWage = 28000m;
        var student = new Student
        {
            Id = 1,
            StudentNumber = "101",
            FirstName = "Ali",
            LastName = "Yılmaz",
            Classroom = "12-A",
            FeeCategory = "kat1", // %30 -> 8.400 TL, günlük 280 TL
            IsActive = true
        };

        var students = new List<Student> { student };
        var enterprises = new List<Enterprise>();

        // 1 gün devamsızlık (D), 3 gün boş gün ("")
        var attendance = new List<AttendanceEntry>
        {
            new() { Year = 2024, Month = 10, StudentId = 1, DayOfMonth = 10, Code = "D" },
            new() { Year = 2024, Month = 10, StudentId = 1, DayOfMonth = 14, Code = "" },
            new() { Year = 2024, Month = 10, StudentId = 1, DayOfMonth = 15, Code = "" },
            new() { Year = 2024, Month = 10, StudentId = 1, DayOfMonth = 16, Code = "" }
        };

        // Act
        var summary = FeeCalculator.CalculateMonthlyFees(2024, 10, minWage, students, enterprises, attendance);

        // Assert
        Assert.Single(summary.Rows);
        var row = summary.Rows[0];
        Assert.Equal(8400m, row.FullWage);
        Assert.Equal(280m, row.DailyWage);
        Assert.Equal(1m, row.DevamsizGunSayisi);
        Assert.Equal(3m, row.BosGunSayisi);
        Assert.Equal(4m, row.ToplamKesintiGunSayisi);
        // Kesinti: 4 gün * 280 TL = 1.120 TL
        Assert.Equal(1120m, row.KesintiTutari);
        // Ödenecek: 8.400 - 1.120 = 7.280 TL
        Assert.Equal(7280m, row.OdenecekTutar);
        Assert.Equal(26m, row.DevamGunSayisi);
        Assert.Equal("Örgün Eğitim", row.EducationType);
    }

    [Fact]
    public void CalculateMonthlyFees_OrgunSeptemberStartDate_ShouldCalculateProRataFromStartDate()
    {
        // Arrange - Örgün Eğitim 14 Eylül başlangıç (14-30 Eylül arası 17 gün)
        const decimal minWage = 28000m;
        var student = new Student
        {
            Id = 1,
            StudentNumber = "101",
            FirstName = "Ali",
            LastName = "Yılmaz",
            Classroom = "12-A",
            FeeCategory = "kat1", // %30 -> 8.400 TL, günlük 280 TL
            IsActive = true
        };

        var students = new List<Student> { student };
        var enterprises = new List<Enterprise>();
        var settings = new AppSettings
        {
            OrgunStartDate = "2026-09-14" // 14 Eylül başlangıç
        };

        // Act - Eylül 2026 (14-30 Eylül arası 17 takvim günü)
        var summary = FeeCalculator.CalculateMonthlyFees(2026, 9, minWage, students, enterprises, new List<AttendanceEntry>(), settings);

        // Assert
        Assert.Single(summary.Rows);
        var row = summary.Rows[0];
        // 17 gün * 280 TL = 4.760 TL
        Assert.Equal(4760m, row.FullWage);
        Assert.Equal(280m, row.DailyWage);
        // Okul öncesi günler devamsızlık sayılmaz, 0 devamsızlık
        Assert.Equal(0m, row.BosGunSayisi);
        Assert.Equal(0m, row.DevamsizGunSayisi);
        Assert.Equal(0m, row.ToplamKesintiGunSayisi);
        Assert.Equal(0m, row.KesintiTutari);
        // Ödenecek net: 4.760 TL
        Assert.Equal(4760m, row.OdenecekTutar);
        Assert.Equal(17m, row.DevamGunSayisi);
    }

    [Fact]
    public void CalculateMonthlyFees_MesemSeptember_ShouldBeFullMonthRegardlessOfStartDate()
    {
        // Arrange - MESEM öğrencisi (yıl boyu kesintisiz, 14 Eylül kısıtı uygulanmaz)
        const decimal minWage = 28000m;
        var student = new Student
        {
            Id = 2,
            StudentNumber = "202",
            FirstName = "Mehmet",
            LastName = "Kaya",
            Classroom = "MESEM 11-A",
            EducationType = "MESEM",
            FeeCategory = "kat1", // %30 -> 8.400 TL
            IsActive = true
        };

        var students = new List<Student> { student };
        var enterprises = new List<Enterprise>();
        var settings = new AppSettings
        {
            OrgunStartDate = "2026-09-14"
        };

        // Act
        var summary = FeeCalculator.CalculateMonthlyFees(2026, 9, minWage, students, enterprises, new List<AttendanceEntry>(), settings);

        // Assert
        Assert.Single(summary.Rows);
        var row = summary.Rows[0];
        // MESEM tam ay (30 gün) = 8.400 TL
        Assert.Equal(8400m, row.FullWage);
        Assert.Equal(280m, row.DailyWage);
        Assert.Equal(0m, row.DevamsizGunSayisi);
        Assert.Equal(0m, row.ToplamKesintiGunSayisi);
        Assert.Equal(0m, row.KesintiTutari);
        Assert.Equal(8400m, row.OdenecekTutar);
        Assert.Equal(30m, row.DevamGunSayisi);
    }

    [Theory]
    [InlineData("Örgün Eğitim", "12-A", "12", true, 0.30, "%30", "Örgün Eğitim")]
    [InlineData("Örgün Eğitim", "11-A", "11", false, 0, "", "")]
    [InlineData("Örgün Eğitim", "10-A", "10", false, 0, "", "")]
    [InlineData("Örgün Eğitim", "9-A", "9", false, 0, "", "")]
    [InlineData("MESEM", "MESEM 9-A", "9", true, 0.30, "%30", "MESEM")]
    [InlineData("MESEM", "MESEM 10-A", "10", true, 0.30, "%30", "MESEM")]
    [InlineData("MESEM", "MESEM 11-A", "11", true, 0.30, "%30", "MESEM")]
    [InlineData("MESEM", "MESEM 12-A", "12", true, 0.50, "1/2", "MESEM")]
    public void CalculateMonthlyFees_GradeAndEducationType_DeterminesCorrectRateAndCategory(
        string educationType,
        string classroom,
        string grade,
        bool isEligible,
        decimal expectedRate,
        string expectedCategoryText,
        string expectedEducationType)
    {
        // Arrange
        const decimal minWage = 28000m;
        var student = new Student
        {
            Id = 1,
            StudentNumber = "101",
            FirstName = "Test",
            LastName = "Öğrenci",
            EducationType = educationType,
            Classroom = classroom,
            Grade = grade,
            IsActive = true
        };

        // Act
        var summary = FeeCalculator.CalculateMonthlyFees(2024, 10, minWage, new List<Student> { student }, new List<Enterprise>(), new List<AttendanceEntry>());

        // Assert
        if (!isEligible)
        {
            Assert.Empty(summary.Rows);
            Assert.Equal(0, summary.StudentCount);
            return;
        }

        Assert.Single(summary.Rows);
        var row = summary.Rows[0];
        decimal expectedFullWage = Math.Round(minWage * expectedRate, 2);
        Assert.Equal(expectedFullWage, row.FullWage);
        Assert.Equal(expectedCategoryText, row.RateText);
        Assert.Equal(expectedCategoryText, row.Category);
        Assert.Equal(expectedEducationType, row.EducationType);
        Assert.DoesNotContain("(", row.RateText);
        Assert.DoesNotContain(")", row.RateText);
        Assert.DoesNotContain("(", row.Category);
        Assert.DoesNotContain(")", row.Category);

        if (expectedCategoryText == "1/2")
        {
            Assert.Equal(1, summary.Kat2Count);
            Assert.Equal(0, summary.Kat1Count);
        }
        else
        {
            Assert.Equal(1, summary.Kat1Count);
            Assert.Equal(0, summary.Kat2Count);
        }
    }

    [Theory]
    [InlineData("11-A", 11, "A")]
    [InlineData("12-B", 12, "B")]
    [InlineData("10/C", 10, "C")]
    [InlineData("MESEM 9-A", 9, "A")]
    [InlineData("MESEM-12B", 12, "B")]
    [InlineData("9A", 9, "A")]
    public void ParseClassAndBranch_ShouldExtractGradeAndBranch(string classroom, int expectedGrade, string expectedBranch)
    {
        var (grade, branch) = FeeCalculator.ParseClassAndBranch(classroom);
        Assert.Equal(expectedGrade, grade);
        Assert.Equal(expectedBranch, branch);
    }

    [Fact]
    public void GetGradeLevel_ClassroomTakesPriorityOverOutdatedGrade()
    {
        // Öğrencinin Grade alanı veritabanından 12 gelse bile, sınıfı 11-A ise seviye 11 olmalıdır
        var student = new Student
        {
            Classroom = "11-A",
            Grade = "12",
            EducationType = "Örgün Eğitim"
        };

        var grade = FeeCalculator.GetGradeLevel(student, null);
        Assert.Equal(11, grade);

        // Örgün eğitimde 11. sınıf listeye giremez
        var isEligible = FeeCalculator.IsEligibleForFeeCalculation(student, null);
        Assert.False(isEligible);
    }

    [Fact]
    public void IsEligibleForFeeCalculation_ShouldFollowClassHasInternshipFlag()
    {
        var student1 = new Student { Classroom = "11-A", EducationType = "Örgün Eğitim" };
        var classWithInternship = new SchoolClass { Name = "11-A", HasInternship = true };
        Assert.True(FeeCalculator.IsEligibleForFeeCalculation(student1, classWithInternship));

        var student2 = new Student { Classroom = "12-B", EducationType = "Örgün Eğitim" };
        var classWithoutInternship = new SchoolClass { Name = "12-B", HasInternship = false };
        Assert.False(FeeCalculator.IsEligibleForFeeCalculation(student2, classWithoutInternship));
    }

    [Fact]
    public void CalculateMonthlyFees_OrgunJuneEndDate_ShouldCalculateProRataUntilEndDate()
    {
        // Arrange - Örgün Eğitim 20 Haziran bitiş (1-20 Haziran arası 20 gün)
        const decimal minWage = 28000m;
        var student = new Student
        {
            Id = 1,
            StudentNumber = "101",
            FirstName = "Ali",
            LastName = "Yılmaz",
            Classroom = "12-A",
            FeeCategory = "kat1", // %30 -> 8.400 TL, günlük 280 TL
            IsActive = true
        };

        var students = new List<Student> { student };
        var enterprises = new List<Enterprise>();
        var settings = new AppSettings
        {
            OrgunEndDate = "2026-06-20" // 20 Haziran bitiş
        };

        // 1 gün devamsızlık (10 Haziran)
        var attendance = new List<AttendanceEntry>
        {
            new() { Year = 2026, Month = 6, StudentId = 1, DayOfMonth = 10, Code = "D" }
        };

        // Act - Haziran 2026
        var summary = FeeCalculator.CalculateMonthlyFees(2026, 6, minWage, students, enterprises, attendance, settings);

        // Assert
        Assert.Single(summary.Rows);
        var row = summary.Rows[0];
        // 20 gün * 280 TL = 5.600 TL brüt hak ediş
        Assert.Equal(5600m, row.FullWage);
        Assert.Equal(280m, row.DailyWage);
        // Sadece 10 Haziran devamsızlığı sayılır (21-30 Haziran sayılmaz)
        Assert.Equal(1m, row.DevamsizGunSayisi);
        Assert.Equal(1m, row.ToplamKesintiGunSayisi);
        // Kesinti: 1 * 280 = 280 TL
        Assert.Equal(280m, row.KesintiTutari);
        // Ödenecek net: 5.600 - 280 = 5.320 TL
        Assert.Equal(5320m, row.OdenecekTutar);
        Assert.Equal(19m, row.DevamGunSayisi);
    }

    [Fact]
    public void CalculateMonthlyFees_StudentWithMidMonthSgkEntryDate_ShouldCalculateProRataFromEntryDate()
    {
        // Arrange - Öğrencinin işe başlama tarihi 20 Eylül (20-30 Eylül arası 11 takvim günü)
        const decimal minWage = 28000m;
        var student = new Student
        {
            Id = 5,
            StudentNumber = "505",
            FirstName = "Can",
            LastName = "Demir",
            Classroom = "12-A",
            FeeCategory = "kat1", // %30 -> 8.400 TL, günlük 280 TL
            SgkEntryDate = new DateTime(2026, 9, 20), // 20 Eylül'de işe başladı
            IsActive = true
        };

        var students = new List<Student> { student };
        var enterprises = new List<Enterprise>();
        var settings = new AppSettings { OrgunStartDate = "2026-09-14" };

        // Act - Eylül 2026
        var summary = FeeCalculator.CalculateMonthlyFees(2026, 9, minWage, students, enterprises, new List<AttendanceEntry>(), settings);

        // Assert
        Assert.Single(summary.Rows);
        var row = summary.Rows[0];
        // 11 gün * 280 TL = 3.080 TL
        Assert.Equal(3080m, row.FullWage);
        Assert.Equal(280m, row.DailyWage);
        // 20 Eylül'den önceki günler devamsızlık sayılmaz
        Assert.Equal(0m, row.DevamsizGunSayisi);
        Assert.Equal(0m, row.BosGunSayisi);
        Assert.Equal(0m, row.KesintiTutari);
        Assert.Equal(3080m, row.OdenecekTutar);
        Assert.Equal(11m, row.DevamGunSayisi);
        Assert.Contains("20-30 Eylül", row.Note);
        Assert.Contains("İşe Giriş: 20.09.2026", row.Note);
    }

    [Fact]
    public void CalculateMonthlyFees_StudentWithFutureSgkEntryDate_ShouldHaveZeroWageAndNotStartedNote()
    {
        // Arrange - Öğrencinin işe başlama tarihi 15 Ekim 2026 (Eylül ayında henüz başlamamış)
        const decimal minWage = 28000m;
        var student = new Student
        {
            Id = 6,
            StudentNumber = "606",
            FirstName = "Deniz",
            LastName = "Arslan",
            Classroom = "MESEM 11-A",
            EducationType = "MESEM",
            FeeCategory = "kat1",
            SgkEntryDate = new DateTime(2026, 10, 15),
            IsActive = true
        };

        var students = new List<Student> { student };

        // Act - Eylül 2026
        var summary = FeeCalculator.CalculateMonthlyFees(2026, 9, minWage, students, new List<Enterprise>(), new List<AttendanceEntry>());

        // Assert
        Assert.Single(summary.Rows);
        var row = summary.Rows[0];
        Assert.Equal(0m, row.FullWage);
        Assert.Equal(0m, row.OdenecekTutar);
        Assert.Equal(0m, row.DevamGunSayisi);
        Assert.Equal(0m, row.DevamsizGunSayisi);
        Assert.Equal(0m, row.BosGunSayisi);
        Assert.Contains("Öğrenci henüz işe başlamamıştır", row.Note);
        Assert.Contains("15.10.2026", row.Note);
    }

    [Fact]
    public void CalculateMonthlyFees_StudentWithMidMonthSgkExitDate_ShouldCalculateProRataUntilExitDate()
    {
        // Arrange - MESEM öğrencisi 10 Eylül 2026'da işten ayrıldı (1-10 Eylül arası 10 gün)
        const decimal minWage = 28000m;
        var student = new Student
        {
            Id = 7,
            StudentNumber = "707",
            FirstName = "Ece",
            LastName = "Çelik",
            Classroom = "MESEM 12-A",
            EducationType = "MESEM",
            Grade = "12", // 1/2 -> %50 -> 14.000 TL, günlük 466.67 TL
            SgkExitDate = new DateTime(2026, 9, 10),
            IsActive = true
        };

        var students = new List<Student> { student };

        // Act - Eylül 2026
        var summary = FeeCalculator.CalculateMonthlyFees(2026, 9, minWage, students, new List<Enterprise>(), new List<AttendanceEntry>());

        // Assert
        Assert.Single(summary.Rows);
        var row = summary.Rows[0];
        // 10 gün * (14.000 / 30) = 4.666,67 TL
        decimal expectedWage = Math.Round(10 * (14000m / 30m), 2);
        Assert.Equal(expectedWage, row.FullWage);
        // 10 Eylül'den sonraki günler devamsızlık sayılmaz
        Assert.Equal(0m, row.DevamsizGunSayisi);
        Assert.Equal(0m, row.BosGunSayisi);
        Assert.Equal(0m, row.KesintiTutari);
        Assert.Equal(expectedWage, row.OdenecekTutar);
        Assert.Equal(10m, row.DevamGunSayisi);
        Assert.Contains("1-10 Eylül", row.Note);
        Assert.Contains("İşten Çıkış: 10.09.2026", row.Note);
    }

    [Fact]
    public void CalculateMonthlyFees_StudentWithPastSgkExitDate_ShouldHaveZeroWageAndExitedNote()
    {
        // Arrange - Öğrenci 31 Ağustos 2026'da işten ayrılmış (Eylül'de aktif değil)
        const decimal minWage = 28000m;
        var student = new Student
        {
            Id = 8,
            StudentNumber = "808",
            FirstName = "Burak",
            LastName = "Yıldız",
            Classroom = "12-B",
            FeeCategory = "kat1",
            SgkExitDate = new DateTime(2026, 8, 31),
            IsActive = true
        };

        var students = new List<Student> { student };

        // Act - Eylül 2026
        var summary = FeeCalculator.CalculateMonthlyFees(2026, 9, minWage, students, new List<Enterprise>(), new List<AttendanceEntry>());

        // Assert
        Assert.Single(summary.Rows);
        var row = summary.Rows[0];
        Assert.Equal(0m, row.FullWage);
        Assert.Equal(0m, row.OdenecekTutar);
        Assert.Equal(0m, row.DevamsizGunSayisi);
        Assert.Equal(0m, row.BosGunSayisi);
        Assert.Contains("Öğrenci işten ayrılmıştır", row.Note);
        Assert.Contains("31.08.2026", row.Note);
    }

    [Theory]
    [InlineData("13", 13)]
    [InlineData("179", 179)]
    [InlineData("40", 40)]
    [InlineData(" 85 ", 85)]
    [InlineData("NO: 99", 99)]
    [InlineData("ABC", int.MaxValue)]
    [InlineData("", int.MaxValue)]
    [InlineData(null, int.MaxValue)]
    public void ParseStudentNumber_ShouldExtractNumericValueCorrectly(string? input, int expected)
    {
        var result = FeeCalculator.ParseStudentNumber(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateMonthlyFees_ShouldSortStudentsNumericallyByStudentNumberWithinClass()
    {
        // Arrange - Öğrenci numaraları alfabetik sıralandığında "179" < "40" olur.
        // Doğal sayısal sıralamada ise 13, 15, 40, 77, 85, 179 olmalıdır.
        const decimal minWage = 28000m;
        var numbers = new[] { "179", "13", "85", "40", "15", "77" };
        var students = numbers.Select((num, idx) => new Student
        {
            Id = idx + 1,
            StudentNumber = num,
            FirstName = "Öğrenci",
            LastName = num,
            Classroom = "12-A",
            FeeCategory = "kat1",
            IsActive = true
        }).ToList();

        // Act
        var summary = FeeCalculator.CalculateMonthlyFees(2026, 10, minWage, students, new List<Enterprise>(), new List<AttendanceEntry>());

        // Assert
        var resultNumbers = summary.Rows.Select(r => r.Student.StudentNumber).ToList();
        Assert.Equal(new[] { "13", "15", "40", "77", "85", "179" }, resultNumbers);
    }

    [Fact]
    public void CalculateMonthlyFees_WhenClassIgnoreHalfDayDeductionIsTrue_HalfDayAbsenceShouldNotBeDeducted()
    {
        // Arrange
        const decimal minWage = 28000m;
        var student = new Student
        {
            Id = 1,
            StudentNumber = "101",
            FirstName = "Ali",
            LastName = "Yılmaz",
            Classroom = "12-A",
            FeeCategory = "kat1", // %30 -> 8.400 TL, günlük 280 TL
            IsActive = true
        };

        var schoolClass = new SchoolClass
        {
            Name = "12-A",
            IgnoreHalfDayDeduction = true // Yarım gün devamsızlığı kesme seçeneği İŞARETLİ
        };

        var students = new List<Student> { student };
        var classes = new List<SchoolClass> { schoolClass };

        // 1 gün SY (Sabah Yarım - 0.5 gün) devamsızlık
        var attendance = new List<AttendanceEntry>
        {
            new() { Year = 2024, Month = 10, StudentId = 1, DayOfMonth = 10, Code = "SY" }
        };

        // Act
        var summary = FeeCalculator.CalculateMonthlyFees(2024, 10, minWage, students, new List<Enterprise>(), attendance, classes: classes);

        // Assert
        Assert.Single(summary.Rows);
        var row = summary.Rows[0];
        Assert.Equal(0m, row.DevamsizGunSayisi); // 0.5 gün kesilmedi, 0 kaldı
        Assert.Equal(0m, row.KesintiTutari);
        Assert.Equal(8400m, row.OdenecekTutar);
    }

    [Fact]
    public void CalculateMonthlyFees_WhenClassIgnoreHalfDayDeductionIsFalse_HalfDayAbsenceShouldBeDeducted()
    {
        // Arrange
        const decimal minWage = 28000m;
        var student = new Student
        {
            Id = 1,
            StudentNumber = "101",
            FirstName = "Ali",
            LastName = "Yılmaz",
            Classroom = "12-A",
            FeeCategory = "kat1", // %30 -> 8.400 TL, günlük 280 TL
            IsActive = true
        };

        var schoolClass = new SchoolClass
        {
            Name = "12-A",
            IgnoreHalfDayDeduction = false // Yarım gün devamsızlığı kes varsayılan
        };

        var students = new List<Student> { student };
        var classes = new List<SchoolClass> { schoolClass };

        // 1 gün SY (Sabah Yarım - 0.5 gün) devamsızlık
        var attendance = new List<AttendanceEntry>
        {
            new() { Year = 2024, Month = 10, StudentId = 1, DayOfMonth = 10, Code = "SY" }
        };

        // Act
        var summary = FeeCalculator.CalculateMonthlyFees(2024, 10, minWage, students, new List<Enterprise>(), attendance, classes: classes);

        // Assert
        Assert.Single(summary.Rows);
        var row = summary.Rows[0];
        Assert.Equal(0.5m, row.DevamsizGunSayisi); // 0.5 gün kesildi
        Assert.Equal(140m, row.KesintiTutari); // 0.5 * 280 TL = 140 TL
        Assert.Equal(8260m, row.OdenecekTutar);
    }

    [Fact]
    public void CalculateMonthlyFees_ShouldCalculateDevletKatkisiAndIsletmeMaliyetiCorrectly()
    {
        // Arrange
        const decimal minWage = 28000m;
        var studentOrgun = new Student
        {
            Id = 1,
            StudentNumber = "101",
            FirstName = "Ali",
            LastName = "Yılmaz",
            Classroom = "12-A",
            EducationType = "Örgün Eğitim",
            FeeCategory = "kat1", // %30 -> 8.400 TL
            IsActive = true
        };
        var studentMesem = new Student
        {
            Id = 2,
            StudentNumber = "102",
            FirstName = "Ayşe",
            LastName = "Demir",
            Classroom = "12-MESEM",
            EducationType = "MESEM",
            FeeCategory = "kat2", // 1/2 -> 14.000 TL
            IsActive = true
        };

        var students = new List<Student> { studentOrgun, studentMesem };

        // Act
        var summary = FeeCalculator.CalculateMonthlyFees(2024, 10, minWage, students, new List<Enterprise>(), new List<AttendanceEntry>());

        // Assert
        Assert.Equal(2, summary.Rows.Count);

        var rowOrgun = summary.Rows.First(r => r.Student.Id == 1);
        // Örgün eğitim: Ödenecek 8.400 TL, 2/3 Devlet Katkısı = 5.600 TL, 1/3 İşletme Maliyeti = 2.800 TL
        Assert.Equal(8400m, rowOrgun.OdenecekTutar);
        Assert.Equal(5600m, rowOrgun.DevletKatkisi);
        Assert.Equal(2800m, rowOrgun.IsletmeMaliyeti);

        var rowMesem = summary.Rows.First(r => r.Student.Id == 2);
        // MESEM: Ödenecek 14.000 TL, Devlet Katkısı = 14.000 TL, İşletme Maliyeti = 0 TL
        Assert.Equal(14000m, rowMesem.OdenecekTutar);
        Assert.Equal(14000m, rowMesem.DevletKatkisi);
        Assert.Equal(0m, rowMesem.IsletmeMaliyeti);
    }

    [Fact]
    public void CalculateMonthlyFees_EnterpriseEmployeeCountAndOptOut_ShouldCalculateStateContributionCorrectly()
    {
        // Arrange
        const decimal minWage = 28000m;
        var ent1 = new Enterprise { Id = 10, Name = "Küçük İşletme (<20)", EmployeeCount = 5, OptOutStateContribution = false };
        var ent2 = new Enterprise { Id = 20, Name = "Büyük İşletme (>=20)", EmployeeCount = 25, OptOutStateContribution = false };
        var ent3 = new Enterprise { Id = 30, Name = "Katkı İstemeyen İşletme", EmployeeCount = 10, OptOutStateContribution = true };

        var st1 = new Student { Id = 1, StudentNumber = "101", FirstName = "Ali", LastName = "A", Classroom = "12-A", EducationType = "Örgün Eğitim", FeeCategory = "kat1", EnterpriseId = 10, IsActive = true };
        var st2 = new Student { Id = 2, StudentNumber = "102", FirstName = "Veli", LastName = "B", Classroom = "12-B", EducationType = "Örgün Eğitim", FeeCategory = "kat1", EnterpriseId = 20, IsActive = true };
        var st3 = new Student { Id = 3, StudentNumber = "103", FirstName = "Can", LastName = "C", Classroom = "12-C", EducationType = "Örgün Eğitim", FeeCategory = "kat1", EnterpriseId = 30, IsActive = true };

        var students = new List<Student> { st1, st2, st3 };
        var enterprises = new List<Enterprise> { ent1, ent2, ent3 };

        // Act
        var summary = FeeCalculator.CalculateMonthlyFees(2024, 10, minWage, students, enterprises, new List<AttendanceEntry>());

        // Assert
        Assert.Equal(3, summary.Rows.Count);

        // st1 (<20 çalışan) -> 8.400 TL maaş, 2/3 Devlet Katkısı = 5.600 TL, 1/3 İşletme Maliyeti = 2.800 TL
        var row1 = summary.Rows.First(r => r.Student.Id == 1);
        Assert.Equal(5600m, row1.DevletKatkisi);
        Assert.Equal(2800m, row1.IsletmeMaliyeti);

        // st2 (>=20 çalışan) -> 8.400 TL maaş, 1/3 Devlet Katkısı = 2.800 TL, 2/3 İşletme Maliyeti = 5.600 TL
        var row2 = summary.Rows.First(r => r.Student.Id == 2);
        Assert.Equal(2800m, row2.DevletKatkisi);
        Assert.Equal(5600m, row2.IsletmeMaliyeti);

        // st3 (OptOutStateContribution == true) -> Devlet Katkısı = 0 TL, İşletme Maliyeti = 8.400 TL
        var row3 = summary.Rows.First(r => r.Student.Id == 3);
        Assert.Equal(0m, row3.DevletKatkisi);
        Assert.Equal(8400m, row3.IsletmeMaliyeti);
    }
}

