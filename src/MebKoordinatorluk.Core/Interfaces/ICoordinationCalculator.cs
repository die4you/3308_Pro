using MebKoordinatorluk.Core.Enums;

namespace MebKoordinatorluk.Core.Interfaces;

/// <summary>
/// MEB Koordinatörlük ek ders saati hesaplama servis sözleşmesi
/// </summary>
public interface ICoordinationCalculator
{
    /// <summary>
    /// Bir işletmedeki öğrenci sayısı ve konumuna göre haftalık koordinatörlük ek ders saatini hesaplar.
    /// </summary>
    /// <param name="studentCount">İşletmedeki öğrenci sayısı</param>
    /// <param name="distanceType">Belediye ve mücavir alan içi / dışı</param>
    /// <returns>Haftalık koordinatörlük ek ders saati</returns>
    double CalculateWeeklyHours(int studentCount, DistanceType distanceType);

    /// <summary>
    /// Bir öğretmenin alabileceği azami koordinatörlük saat sınırını denetler.
    /// </summary>
    /// <param name="currentTotalHours">Öğretmenin mevcut dağıtılmış toplam koordinatörlük saati</param>
    /// <param name="additionalHours">Eklenecek saat</param>
    /// <param name="maxAllowedHours">Öğretmenin azami alabileceği koordinatörlük saati (varsayılan 16)</param>
    /// <returns>Sınır aşımı kontrol sonucu</returns>
    bool CanAssignHours(double currentTotalHours, double additionalHours, int maxAllowedHours);
}
