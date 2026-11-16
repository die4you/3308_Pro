using MebKoordinatorluk.Core.Services;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class SgkDateValidatorTests
{
    [Fact]
    public void GetDefaultSgkEntryDate_OnMonday_ReturnsSameDay()
    {
        // Monday 2026-10-12
        var monday = new DateTime(2026, 10, 12);
        var result = SgkDateValidator.GetDefaultSgkEntryDate(monday);
        Assert.Equal(monday, result);
    }

    [Fact]
    public void GetDefaultSgkEntryDate_OnTuesday_ReturnsWednesday()
    {
        // Tuesday 2026-10-13
        var tuesday = new DateTime(2026, 10, 13);
        var expected = new DateTime(2026, 10, 14); // Wednesday
        var result = SgkDateValidator.GetDefaultSgkEntryDate(tuesday);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetDefaultSgkEntryDate_OnFriday_ReturnsNextMonday()
    {
        // Friday 2026-10-16
        var friday = new DateTime(2026, 10, 16);
        var expected = new DateTime(2026, 10, 19); // Monday
        var result = SgkDateValidator.GetDefaultSgkEntryDate(friday);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetDefaultSgkEntryDate_OnSaturday_ReturnsNextMonday()
    {
        // Saturday 2026-10-17
        var saturday = new DateTime(2026, 10, 17);
        var expected = new DateTime(2026, 10, 19); // Monday
        var result = SgkDateValidator.GetDefaultSgkEntryDate(saturday);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ValidateSgkEntryDate_PastDate_Fails()
    {
        var today = new DateTime(2026, 10, 12); // Monday
        var pastDate = new DateTime(2026, 10, 11);
        
        bool valid = SgkDateValidator.ValidateSgkEntryDate(pastDate, today, out var error);
        Assert.False(valid);
        Assert.Contains("geçmiş bir tarih olamaz", error);
    }

    [Fact]
    public void ValidateSgkEntryDate_Weekend_Fails()
    {
        var monday = new DateTime(2026, 10, 12);
        var saturday = new DateTime(2026, 10, 17);

        bool valid = SgkDateValidator.ValidateSgkEntryDate(saturday, monday, out var error);
        Assert.False(valid);
        Assert.Contains("hafta sonu", error);
    }

    [Fact]
    public void ValidateSgkEntryDate_OnMonday_SameDay_Passes()
    {
        var monday = new DateTime(2026, 10, 12);

        bool valid = SgkDateValidator.ValidateSgkEntryDate(monday, monday, out var error);
        Assert.True(valid);
        Assert.Empty(error);
    }

    [Fact]
    public void ValidateSgkEntryDate_OnTuesday_SameDay_Fails()
    {
        var tuesday = new DateTime(2026, 10, 13);

        bool valid = SgkDateValidator.ValidateSgkEntryDate(tuesday, tuesday, out var error);
        Assert.False(valid);
        Assert.Contains("en erken ertesi iş günü", error);
    }

    [Fact]
    public void ValidateSgkEntryDate_OnTuesday_NextDay_Passes()
    {
        var tuesday = new DateTime(2026, 10, 13);
        var wednesday = new DateTime(2026, 10, 14);

        bool valid = SgkDateValidator.ValidateSgkEntryDate(wednesday, tuesday, out var error);
        Assert.True(valid);
        Assert.Empty(error);
    }

    [Fact]
    public void ValidateSgkExitDate_NoEntryDate_Fails()
    {
        var today = new DateTime(2026, 10, 15);
        var exitDate = new DateTime(2026, 10, 14);

        bool valid = SgkDateValidator.ValidateSgkExitDate(exitDate, null, today, out var error);
        Assert.False(valid);
        Assert.Contains("giriş tarihi bulunmayan", error);
    }

    [Fact]
    public void ValidateSgkExitDate_ExitBeforeOrSameAsEntry_Fails()
    {
        var today = new DateTime(2026, 10, 15);
        var entryDate = new DateTime(2026, 10, 10);
        var exitDateSame = new DateTime(2026, 10, 10);
        var exitDateBefore = new DateTime(2026, 10, 09);

        bool validSame = SgkDateValidator.ValidateSgkExitDate(exitDateSame, entryDate, today, out var errorSame);
        Assert.False(validSame);
        Assert.Contains("sonraki bir tarih olmalıdır", errorSame);

        bool validBefore = SgkDateValidator.ValidateSgkExitDate(exitDateBefore, entryDate, today, out var errorBefore);
        Assert.False(validBefore);
        Assert.Contains("sonraki bir tarih olmalıdır", errorBefore);
    }

    [Fact]
    public void ValidateSgkExitDate_MoreThan10DaysRetroactive_Fails()
    {
        var today = new DateTime(2026, 10, 20);
        var entryDate = new DateTime(2026, 9, 1);
        var oldExitDate = new DateTime(2026, 10, 5); // 15 days ago (> 10 days)

        bool valid = SgkDateValidator.ValidateSgkExitDate(oldExitDate, entryDate, today, out var error);
        Assert.False(valid);
        Assert.Contains("en fazla 10 gün geriye dönük", error);
    }

    [Fact]
    public void ValidateSgkExitDate_ValidWithin10DaysAndAfterEntry_Passes()
    {
        var today = new DateTime(2026, 10, 20);
        var entryDate = new DateTime(2026, 10, 1);
        var exitDate = new DateTime(2026, 10, 15); // 5 days ago (within 10 days)

        bool valid = SgkDateValidator.ValidateSgkExitDate(exitDate, entryDate, today, out var error);
        Assert.True(valid);
        Assert.Empty(error);
    }
}
