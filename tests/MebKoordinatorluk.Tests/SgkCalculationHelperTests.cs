using System;
using MebKoordinatorluk.Core.Services;
using Xunit;

namespace MebKoordinatorluk.Tests;

public class SgkCalculationHelperTests
{
    [Fact]
    public void Calculate_31DayMonth_ZeroAbsence_Returns30PaidAnd0Missing()
    {
        var result = SgkCalculationHelper.Calculate(2026, 10, null, null, 0);
        Assert.Equal(30, result.BildirilecekPrimGunu);
        Assert.Equal(0, result.BildirilecekEksikGun);
    }

    [Fact]
    public void Calculate_31DayMonth_1DayAbsence_Returns30PaidAnd0Missing_30GunKurali()
    {
        // 31 çeken ayda 1 gün devamsızlık 31. günü eritir: 30 gün prim ve 0 eksik gün bildirilir.
        var result = SgkCalculationHelper.Calculate(2026, 10, null, null, 1);
        Assert.Equal(30, result.BildirilecekPrimGunu);
        Assert.Equal(0, result.BildirilecekEksikGun);
    }

    [Fact]
    public void Calculate_31DayMonth_2DaysAndAboveAbsence_WritesAbsenceDirectlyToMissingDays()
    {
        // 31 çeken aylarda 2 gün ve üzeri devamsızlık doğrudan eksik gün sütununa yazılır:
        // 2 gün devamsızlık -> 29 prim, 2 eksik gün
        var res2 = SgkCalculationHelper.Calculate(2026, 10, null, null, 2);
        Assert.Equal(29, res2.BildirilecekPrimGunu);
        Assert.Equal(2, res2.BildirilecekEksikGun);

        // 5 gün devamsızlık -> 26 prim, 5 eksik gün
        var res5 = SgkCalculationHelper.Calculate(2026, 10, null, null, 5);
        Assert.Equal(26, res5.BildirilecekPrimGunu);
        Assert.Equal(5, res5.BildirilecekEksikGun);

        // 15 gün devamsızlık -> 16 prim, 15 eksik gün
        var res15 = SgkCalculationHelper.Calculate(2026, 10, null, null, 15);
        Assert.Equal(16, res15.BildirilecekPrimGunu);
        Assert.Equal(15, res15.BildirilecekEksikGun);
    }

    [Fact]
    public void Calculate_30DayMonth_Absence_ReturnsCorrectDays()
    {
        // 30 çeken ayda 0 devamsızlık -> 30 prim, 0 eksik
        var res0 = SgkCalculationHelper.Calculate(2026, 4, null, null, 0);
        Assert.Equal(30, res0.BildirilecekPrimGunu);
        Assert.Equal(0, res0.BildirilecekEksikGun);

        // 30 çeken ayda 1 devamsızlık -> 29 prim, 1 eksik
        var res1 = SgkCalculationHelper.Calculate(2026, 4, null, null, 1);
        Assert.Equal(29, res1.BildirilecekPrimGunu);
        Assert.Equal(1, res1.BildirilecekEksikGun);

        // 30 çeken ayda 2 devamsızlık -> 28 prim, 2 eksik
        var res2 = SgkCalculationHelper.Calculate(2026, 4, null, null, 2);
        Assert.Equal(28, res2.BildirilecekPrimGunu);
        Assert.Equal(2, res2.BildirilecekEksikGun);
    }

    [Fact]
    public void Calculate_February28Days_Absence_ReturnsCorrectDays()
    {
        // Şubat 28 çeken ayda 0 devamsızlık -> 30 prim, 0 eksik
        var res0 = SgkCalculationHelper.Calculate(2026, 2, null, null, 0);
        Assert.Equal(30, res0.BildirilecekPrimGunu);
        Assert.Equal(0, res0.BildirilecekEksikGun);

        // Şubat 28 çeken ayda 1 devamsızlık -> 27 prim, 1 eksik
        var res1 = SgkCalculationHelper.Calculate(2026, 2, null, null, 1);
        Assert.Equal(27, res1.BildirilecekPrimGunu);
        Assert.Equal(1, res1.BildirilecekEksikGun);
    }

    [Fact]
    public void Calculate_MidMonthEntry_NoAbsenceBeforeEntry_MissingDaysIsZero()
    {
        // İşe giriş 5 Ekim 2026 (Sıla Erdal senaryosu): Aktif 27 gün, 0 devamsızlık
        // Giriş öncesi günler için eksik gün yazılmaz!
        var resSila = SgkCalculationHelper.Calculate(2026, 10, new DateTime(2026, 10, 5), null, 0);
        Assert.Equal(27, resSila.BildirilecekPrimGunu);
        Assert.Equal(0, resSila.BildirilecekEksikGun);

        // İşe giriş 19 Ekim 2026 (Irmak Gayretli senaryosu): Aktif 13 gün, 0 devamsızlık
        var resIrmak = SgkCalculationHelper.Calculate(2026, 10, new DateTime(2026, 10, 19), null, 0);
        Assert.Equal(13, resIrmak.BildirilecekPrimGunu);
        Assert.Equal(0, resIrmak.BildirilecekEksikGun);
    }

    [Fact]
    public void Calculate_MidMonthEntry_WithAbsenceInActivePeriod_OnlyActiveAbsenceIsMissingDays()
    {
        // İşe giriş 5 Ekim 2026, aktif dönemde 2 gün devamsızlık:
        // Prim = 27 - 2 = 25 gün, Eksik = 2 gün
        var res = SgkCalculationHelper.Calculate(2026, 10, new DateTime(2026, 10, 5), null, 2);
        Assert.Equal(25, res.BildirilecekPrimGunu);
        Assert.Equal(2, res.BildirilecekEksikGun);
    }
}
