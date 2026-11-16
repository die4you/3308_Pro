using MebKoordinatorluk.Core.Entities;
using MebKoordinatorluk.Core.Services;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class AttendanceCodeHelperTests
{
    [Fact]
    public void AttendanceCodes_DoNotContainG()
    {
        // G (Geç) çizelgeye girilmeyecek kuralı
        var codes = AttendanceCodeHelper.AllCodes;
        Assert.DoesNotContain(codes, c => c.Code == "G");
    }

    [Theory]
    [InlineData("N")]
    [InlineData("F")]
    public void N_And_F_DoNotDeductFeeOrAbsence(string code)
    {
        // N (Nöbetçi) ve F (Faaliyet) için devamsızlık ve maaş/SGK kesintisi 0 olmalı
        Assert.True(AttendanceCodeHelper.IsDutyOrActivity(code));
        var info = AttendanceCodeHelper.GetInfo(code);
        Assert.NotNull(info);
        Assert.False(info!.DeductsFee);
        Assert.Equal(0.0, AttendanceCodeHelper.GetAbsenceDays(code));
        Assert.False(AttendanceCodeHelper.IsUnexcused(code));
        Assert.False(AttendanceCodeHelper.IsExcused(code));
    }

    [Theory]
    [InlineData("ÖY", 0.5)]
    [InlineData("SY", 0.5)]
    [InlineData("Y", 0.5)]
    [InlineData("D", 1.0)]
    public void UnexcusedCodes_ReturnCorrectAbsenceDays(string code, double expectedDays)
    {
        Assert.True(AttendanceCodeHelper.IsUnexcused(code));
        var info = AttendanceCodeHelper.GetInfo(code);
        Assert.NotNull(info);
        Assert.True(info!.DeductsFee);
        Assert.Equal(expectedDays, AttendanceCodeHelper.GetAbsenceDays(code));
    }

    [Theory]
    [InlineData("ÖY", 0.0)]
    [InlineData("SY", 0.0)]
    [InlineData("Y", 0.0)]
    [InlineData("D", 1.0)]
    [InlineData("S", 1.0)]
    [InlineData("İ", 1.0)]
    public void GetSgkAbsenceDays_HalfDaysDoNotDeductSgk(string code, double expectedSgkDays)
    {
        // Günün bölünmezliği ilkesi gereği yarım günler SGK prim gününü kesmez
        Assert.Equal(expectedSgkDays, AttendanceCodeHelper.GetSgkAbsenceDays(code));
    }

    [Theory]
    [InlineData("S", true)]
    [InlineData("SH", true)]
    [InlineData("K", true)]
    [InlineData("İ", false)]
    [InlineData("SV", false)]
    public void HealthReports_IdentifiedCorrectly(string code, bool isReport)
    {
        Assert.Equal(isReport, AttendanceCodeHelper.IsHealthReport(code));
    }

    [Fact]
    public void FeeCalculator_WithNAndF_DoesNotDeductSalary()
    {
        const decimal minWage = 33030.00m;
        var student = new Student
        {
            Id = 1,
            StudentNumber = "101",
            FirstName = "Ali",
            LastName = "Yılmaz",
            Classroom = "12-A",
            FeeCategory = "kat1",
            IsActive = true
        };

        var students = new List<Student> { student };
        var enterprises = new List<Enterprise>();

        // Öğrenci 5. gün N (Nöbetçi), 6. gün F (Faaliyet), 7. gün X (Mevcut)
        var attendance = new List<AttendanceEntry>
        {
            new() { Year = 2026, Month = 10, StudentId = 1, DayOfMonth = 5, Code = "N" },
            new() { Year = 2026, Month = 10, StudentId = 1, DayOfMonth = 6, Code = "F" },
            new() { Year = 2026, Month = 10, StudentId = 1, DayOfMonth = 7, Code = "X" },
        };

        var summary = FeeCalculator.CalculateMonthlyFees(2026, 10, minWage, students, enterprises, attendance);

        Assert.Single(summary.Rows);
        var row = summary.Rows[0];
        // N ve F için devamsızlık kesintisi olmamalı
        Assert.Equal(0m, row.DevamsizGunSayisi);
        Assert.Equal(0m, row.KesintiTutari);
    }

    [Fact]
    public void DetermineSgkMissingReasonCode_SingleReason_ReturnsStandardCode()
    {
        // Yalnızca rapor varsa -> 01
        Assert.Equal("01", AttendanceCodeHelper.DetermineSgkMissingReasonCode(new[] { "S" }));
        Assert.Equal("01", AttendanceCodeHelper.DetermineSgkMissingReasonCode(new[] { "S", "SH", "K" }));

        // Yalnızca özürsüz devamsızlık varsa -> 15
        Assert.Equal("15", AttendanceCodeHelper.DetermineSgkMissingReasonCode(new[] { "D" }));
        Assert.Equal("15", AttendanceCodeHelper.DetermineSgkMissingReasonCode(new[] { "D", "D" }));

        // Yalnızca izin/mazeret varsa -> 13
        Assert.Equal("13", AttendanceCodeHelper.DetermineSgkMissingReasonCode(new[] { "İ" }));
        Assert.Equal("13", AttendanceCodeHelper.DetermineSgkMissingReasonCode(new[] { "İ", "V", "DA" }));

        // Yalnızca disiplin cezası varsa -> 03
        Assert.Equal("03", AttendanceCodeHelper.DetermineSgkMissingReasonCode(new[] { "C" }));
    }

    [Fact]
    public void DetermineSgkMissingReasonCode_MultipleReasons_Returns12()
    {
        // Rapor (01) + Devamsızlık (15) -> 12 (Birden Fazla)
        Assert.Equal("12", AttendanceCodeHelper.DetermineSgkMissingReasonCode(new[] { "S", "D" }));

        // İzin (13) + Devamsızlık (15) -> 12 (Birden Fazla)
        Assert.Equal("12", AttendanceCodeHelper.DetermineSgkMissingReasonCode(new[] { "İ", "D" }));

        // Rapor (01) + İzin (13) -> 12 (Birden Fazla)
        Assert.Equal("12", AttendanceCodeHelper.DetermineSgkMissingReasonCode(new[] { "S", "İ" }));

        // Rapor (01) + Devamsızlık (15) + İzin (13) -> 12 (Birden Fazla)
        Assert.Equal("12", AttendanceCodeHelper.DetermineSgkMissingReasonCode(new[] { "S", "D", "İ" }));
    }
}
