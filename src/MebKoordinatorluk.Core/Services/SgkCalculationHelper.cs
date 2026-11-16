using System;

namespace MebKoordinatorluk.Core.Services;

/// <summary>
/// SGK Bildirge gün ve eksik gün hesaplama çıktısı.
/// </summary>
public record SgkDayCalculationResult(
    int BildirilecekPrimGunu,
    int BildirilecekEksikGun,
    int AyGunSayisi,
    string Aciklama);

/// <summary>
/// SGK Mevzuatı ve e-Bildirge standartlarına göre prim ödeme gün sayısı (GUN)
/// ve eksik gün sayısını (EKSIKGUNSAYISI) hesaplayan servis modülü.
/// </summary>
public static class SgkCalculationHelper
{
    /// <summary>
    /// SGK mevzuatı ve e-Bildirge standartlarına göre prim günü ve eksik gün sayısını hesaplar:
    /// 
    /// 1. Tam Çalışma Kuralı:
    ///    - Ay kaç çekerse çeksin (28, 29, 30, 31 gün), ay içinde eksik günü, işe girişi veya çıkışı olmayan için:
    ///      * bildirilecek_prim_gunu = 30
    ///      * bildirilecek_eksik_gun = 0
    /// 
    /// 2. Ay İçi Eksik Gün Kuralı (SGK Mevzuatı ve Kullanıcı Kuralı):
    ///    - 31 çeken aylarda 1 gün devamsızlık: 31 - 1 = 30 gün prim (GUN), 0 gün eksik gün (30 gün kuralı)
    ///    - 31 çeken aylarda 2 gün ve üzeri devamsızlık: eksik gün kadar eksik gün (EKSIKGUNSAYISI),
    ///      kalan gün kadar prim günü (GUN = 31 - eksik_gun) bildirilir. (Örn: 2 gün devamsızlık -> 29 prim, 2 eksik gün; 15 gün devamsızlık -> 16 prim, 15 eksik gün)
    ///    - 30 çeken aylarda: eksik gün kadar eksik gün, kalan prim günü (GUN = 30 - eksik_gun) bildirilir. (Örn: 1 gün -> 29 prim, 1 eksik; 2 gün -> 28 prim, 2 eksik)
    ///    - 28 veya 29 çeken Şubat ayında: eksik gün kadar eksik gün, kalan prim günü (GUN = ay_gun_sayisi - eksik_gun) bildirilir.
    /// 
    /// 3. İşe Giriş ve İşten Çıkış Halleri (Kıst Dönem):
    ///    - Başlama/çıkış tarihlerine göre aktif çalışma süresi hesaplanır:
    ///      * aktif_gun_sayisi = (cikis_gunu veya ay_gun_sayisi) - (giris_gunu veya 1) + 1
    ///      * prim_gunu = Math.Min(30, Math.Max(0, aktif_gun_sayisi - eksik_donemdeki_gun))
    ///    - İşe girişten önceki veya işten çıkıştan sonraki süreler sigortalılık dışı olduğundan
    ///      bu süreler için EKSİK GÜN HESAPLANMAZ (bildirilmez).
    ///    - Yalnızca aktif çalışma süresinde fiili bir devamsızlık/rapor varsa o devamsızlık kadar eksik gün yazılır.
    ///      (Fiili devamsızlık yoksa: bildirilecek_eksik_gun = 0)
    /// </summary>
    public static SgkDayCalculationResult Calculate(
        int yil,
        int ay,
        DateTime? girisTarihi = null,
        DateTime? cikisTarihi = null,
        int eksikGunSayisi = 0)
    {
        int ayGunSayisi = DateTime.DaysInMonth(yil, ay);
        const int totalMonthDays = 30; // SGK standart aylık tavan gün sayısı
        DateTime ayBasi = new DateTime(yil, ay, 1);
        DateTime aySonu = new DateTime(yil, ay, ayGunSayisi);

        // Ay içinde işe giriş ve işten çıkış durumları
        bool ayIciGiris = girisTarihi.HasValue && girisTarihi.Value.Year == yil && girisTarihi.Value.Month == ay;
        bool ayIciCikis = cikisTarihi.HasValue && cikisTarihi.Value.Year == yil && cikisTarihi.Value.Month == ay;

        // Dönem dışı kontroller (ilgili ayda henüz başlamamış veya çoktan ayrılmış olanlar)
        if (girisTarihi.HasValue && girisTarihi.Value.Date > aySonu)
        {
            return new SgkDayCalculationResult(0, 0, ayGunSayisi, "İşe giriş tarihi ilgili aydan sonra (Dönem dışı).");
        }
        if (cikisTarihi.HasValue && cikisTarihi.Value.Date < ayBasi)
        {
            return new SgkDayCalculationResult(0, 0, ayGunSayisi, "İşten çıkış tarihi ilgili aydan önce (Dönem dışı).");
        }

        // 3. İşe Giriş ve İşten Çıkış Halleri (Kıst Dönem)
        if (ayIciGiris || ayIciCikis)
        {
            int girisGunu = ayIciGiris ? girisTarihi!.Value.Day : 1;
            int cikisGunu = ayIciCikis ? cikisTarihi!.Value.Day : ayGunSayisi;

            int aktifGunSayisi = Math.Max(0, cikisGunu - girisGunu + 1);
            int primGunu = Math.Min(totalMonthDays, Math.Max(0, aktifGunSayisi - eksikGunSayisi));

            // İşe girişten önceki veya çıkıştan sonraki süreler için eksik gün yazılmaz.
            // Sadece aktif çalışma dönemindeki fiili devamsızlık/rapor eksik gün olarak bildirilir.
            int bildirilecekEksikGun = Math.Max(0, eksikGunSayisi);

            string aciklama = $"Kıst Dönem (Giriş: {girisGunu}, Çıkış: {cikisGunu}): Aktif {aktifGunSayisi} gün üzerinden {primGunu} prim günü hesaplandı. Giriş öncesi/çıkış sonrası süreler için eksik gün yazılmadı (Eksik Gün: {bildirilecekEksikGun}).";
            return new SgkDayCalculationResult(primGunu, bildirilecekEksikGun, ayGunSayisi, aciklama);
        }

        // 1. Tam Çalışma Kuralı (Eksik gün yok, giriş/çıkış yok)
        if (eksikGunSayisi <= 0)
        {
            return new SgkDayCalculationResult(
                totalMonthDays,
                0,
                ayGunSayisi,
                $"Tam Çalışma Kuralı: Ay {ayGunSayisi} gün çekmesine rağmen eksik gün ve giriş/çıkış bulunmadığından 30 gün prim ve 0 gün eksik gün bildirilir.");
        }

        // 2. Ay İçi Eksik Gün Hesabı (Devamsızlık, Rapor vb.)
        if (ayGunSayisi == 31)
        {
            if (eksikGunSayisi == 1)
            {
                // 31 çeken ayda 1 gün devamsızlık: 31 - 1 = 30 gün prim.
                // SGK sisteminde 30 gün prim bildirildiğinde eksik gün hanesi doldurulamaz (30 gün kuralı).
                return new SgkDayCalculationResult(
                    30,
                    0,
                    ayGunSayisi,
                    "31 çeken ayda 1 gün devamsızlık: 30 gün kuralı (31 - 1 = 30 prim günü, 0 eksik gün).");
            }
            else
            {
                // 31 çeken aylarda 2 gün ve üzeri devamsızlıklar doğrudan eksik gün sayısına yazılır
                int bildirilecekEksikGun = Math.Min(31, eksikGunSayisi);
                int primGunu = Math.Max(0, 31 - bildirilecekEksikGun);
                return new SgkDayCalculationResult(
                    primGunu,
                    bildirilecekEksikGun,
                    ayGunSayisi,
                    $"31 çeken ayda {eksikGunSayisi} gün devamsızlık: {primGunu} prim günü, {bildirilecekEksikGun} eksik gün bildirilir.");
            }
        }
        else if (ayGunSayisi == 30)
        {
            int bildirilecekEksikGun = Math.Min(30, eksikGunSayisi);
            int primGunu = Math.Max(0, 30 - bildirilecekEksikGun);
            return new SgkDayCalculationResult(
                primGunu,
                bildirilecekEksikGun,
                ayGunSayisi,
                $"30 çeken ayda {eksikGunSayisi} gün devamsızlık: {primGunu} prim günü, {bildirilecekEksikGun} eksik gün bildirilir.");
        }
        else
        {
            // Şubat ayı (28 veya 29 gün)
            int bildirilecekEksikGun = Math.Min(ayGunSayisi, eksikGunSayisi);
            int primGunu = Math.Max(0, ayGunSayisi - bildirilecekEksikGun);
            return new SgkDayCalculationResult(
                primGunu,
                bildirilecekEksikGun,
                ayGunSayisi,
                $"Şubat ayında ({ayGunSayisi} gün) {eksikGunSayisi} gün devamsızlık: {primGunu} prim günü, {bildirilecekEksikGun} eksik gün bildirilir.");
        }
    }

    /// <summary>
    /// XML e-Bildirge ve çapraz denetim için geriye dönük uyumlu yardımcı metod.
    /// </summary>
    public static (int PaidDays, int MissingDays) CalculateSgkDays(
        int year,
        int monthIndex,
        DateTime? entryDate,
        DateTime? exitDate,
        int absentDays)
    {
        var res = Calculate(year, monthIndex, entryDate, exitDate, absentDays);
        return (res.BildirilecekPrimGunu, res.BildirilecekEksikGun);
    }
}
