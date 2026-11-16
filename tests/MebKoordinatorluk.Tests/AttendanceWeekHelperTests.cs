using MebKoordinatorluk.Core.Services;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class AttendanceWeekHelperTests
{
    [Fact]
    public void GetMonthWeekGroups_October2026_ShouldMatchExpectedGroups()
    {
        // Ekim 2026: 1 Ekim Perşembe, 2 Ekim Cuma, 3-4 Ekim Haftasonu.
        // 5 Ekim ilk Pazartesi -> 1. Hafta (5-9) olmalı.
        var groups = AttendanceWeekHelper.GetMonthWeekGroups(2026, 10);

        Assert.NotNull(groups);
        Assert.Equal(5, groups.Count);

        // Grup 0: Geçiş (1-2)
        Assert.True(groups[0].IsTransition);
        Assert.Equal(1, groups[0].StartDay);
        Assert.Equal(4, groups[0].EndDay);
        Assert.Equal("Geçiş (1-2)", groups[0].Title);

        // Grup 1: 1. Hafta (5-9)
        Assert.False(groups[1].IsTransition);
        Assert.Equal(5, groups[1].StartDay);
        Assert.Equal(11, groups[1].EndDay);
        Assert.Equal("1. Hafta (5-9)", groups[1].Title);

        // Grup 2: 2. Hafta (12-16)
        Assert.Equal("2. Hafta (12-16)", groups[2].Title);
        Assert.Equal(12, groups[2].StartDay);
        Assert.Equal(18, groups[2].EndDay);

        // Grup 3: 3. Hafta (19-23)
        Assert.Equal("3. Hafta (19-23)", groups[3].Title);
        Assert.Equal(19, groups[3].StartDay);
        Assert.Equal(25, groups[3].EndDay);

        // Grup 4: 4. Hafta (26-30)
        Assert.Equal("4. Hafta (26-30)", groups[4].Title);
        Assert.Equal(26, groups[4].StartDay);
        Assert.Equal(31, groups[4].EndDay);
    }

    [Fact]
    public void GetMonthWeekGroups_September2025_StartsOnMonday_ShouldNotHaveTransition()
    {
        // Eylül 2025: 1 Eylül Pazartesi
        var groups = AttendanceWeekHelper.GetMonthWeekGroups(2025, 9);

        Assert.NotNull(groups);
        Assert.Equal(5, groups.Count);

        // 1. Hafta doğrudan 1. günden başlamalı
        Assert.False(groups[0].IsTransition);
        Assert.Equal("1. Hafta (1-5)", groups[0].Title);
        Assert.Equal(1, groups[0].StartDay);
        Assert.Equal(7, groups[0].EndDay);
    }

    [Theory]
    [InlineData(2026, 10, 30)] // 31 Ekim 2026 Cumartesi -> En son iş günü 30 Ekim 2026 Cuma
    [InlineData(2026, 5, 29)]  // 31 Mayıs 2026 Pazar -> En son iş günü 29 Mayıs 2026 Cuma
    [InlineData(2026, 4, 30)]  // 30 Nisan 2026 Perşembe -> En son iş günü 30 Nisan 2026 Perşembe
    [InlineData(2021, 10, 28)] // 31 Ekim 2021 Pazar, 30 Cmt, 29 Ekim Resmi Tatil -> 28 Ekim 2021 Perşembe
    public void GetLastWorkingDayOfMonth_ShouldReturnCorrectBusinessDay(int year, int month, int expectedDay)
    {
        var result = AttendanceWeekHelper.GetLastWorkingDayOfMonth(new DateTime(year, month, 1));
        Assert.Equal(new DateTime(year, month, expectedDay), result);
    }
}
