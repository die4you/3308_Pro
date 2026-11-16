namespace MebKoordinatorluk.Core.Enums;

/// <summary>
/// İşletmenin bulunduğu konum ve mücavir alan türü (MEB ek ders koordinatörlük saat hesaplamalarında mesafe çarpanı için kullanılır)
/// </summary>
public enum DistanceType
{
    /// <summary>
    /// Belediye ve mücavir alan sınırları içinde
    /// </summary>
    MucavirAlanIci = 1,

    /// <summary>
    /// Belediye ve mücavir alan sınırları dışında (Büyükşehir/İlçe dışı veya uzak mesafe)
    /// </summary>
    MucavirAlanDisi = 2
}
