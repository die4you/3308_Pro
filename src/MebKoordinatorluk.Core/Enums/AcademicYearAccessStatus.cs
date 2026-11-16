namespace MebKoordinatorluk.Core.Enums;

public enum AcademicYearAccessStatus
{
    ActiveFull,       // Aktif lisanslı eğitim öğretim yılı (Tam yetki: Okuma, Yazma, Düzenleme)
    ArchiveReadOnly,  // Geçmiş eğitim öğretim yılı (Arşiv modu: Salt okunur, inceleme serbest)
    FutureLocked,     // Gelecek eğitim öğretim yılı (Lisans süresi yetersiz, kilitli - 3308pro@gmail.com)
    DemoAccess        // Lisanssız veya süresi dolmuş demo modu (Mevcut yıl için 5 öğrenci / 3 işletme sınırı)
}
