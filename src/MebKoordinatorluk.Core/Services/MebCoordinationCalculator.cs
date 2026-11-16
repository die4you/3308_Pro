using MebKoordinatorluk.Core.Enums;
using MebKoordinatorluk.Core.Interfaces;

namespace MebKoordinatorluk.Core.Services;

/// <summary>
/// MEB Yönetici ve Öğretmenlerinin Ders ve Ek Ders Saatlerine İlişkin Karar ve Yönetmelik Kurallarına Göre Hesaplayıcı
/// </summary>
public class MebCoordinationCalculator : ICoordinationCalculator
{
    public double CalculateWeeklyHours(int studentCount, DistanceType distanceType)
    {
        if (studentCount <= 0)
            return 0;

        // MEB Standart Dilimleri (Grup / Öğrenci Sayısına Göre):
        // 1-15 öğrenci: 4 saat
        // 16-20 öğrenci: 5 saat
        // 21-25 öğrenci: 6 saat
        // 26-30 öğrenci: 7 saat
        // 31+ öğrenci: her 5 öğrenci için +1 saat
        double baseHours;
        if (studentCount <= 15)
        {
            baseHours = 4.0;
        }
        else if (studentCount <= 20)
        {
            baseHours = 5.0;
        }
        else if (studentCount <= 25)
        {
            baseHours = 6.0;
        }
        else if (studentCount <= 30)
        {
            baseHours = 7.0;
        }
        else
        {
            baseHours = 7.0 + Math.Ceiling((studentCount - 30) / 5.0);
        }

        // Mücavir alan dışı işletmeler için yol ve mesafe ek koordinatörlük katsayısı/saati (+2 saat veya 1.25 çarpanı)
        if (distanceType == DistanceType.MucavirAlanDisi)
        {
            baseHours += 2.0; // Mücavir alan dışı ek süre
        }

        return baseHours;
    }

    public bool CanAssignHours(double currentTotalHours, double additionalHours, int maxAllowedHours)
    {
        return (currentTotalHours + additionalHours) <= maxAllowedHours;
    }
}
