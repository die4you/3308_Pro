namespace MebKoordinatorluk.Core.Entities;

public class AppSettings
{
    public string Valilik { get; set; } = "İSTANBUL VALİLİĞİ";
    public string DistrictMem { get; set; } = "İlçe Millî Eğitim Müdürlüğü";
    public string KurumKodu { get; set; } = "";
    public string SchoolName { get; set; } = "MİMAR SİNAN MESLEKİ VE TEKNİK ANADOLU LİSESİ";
    public string SayiNo { get; set; } = "E-84125896-121.01-4587";
    public string FormDate { get; set; } = DateTime.Now.ToString("dd/MM/yyyy");
    public string ValidityDate { get; set; } = DateTime.Now.ToString("dd/MM/yyyy");
    public string TeacherTitle { get; set; } = "";
    public string AcademicYear { get; set; } = "2024-2025";
    public string Term { get; set; } = "1.";
    public string FormBodyText { get; set; } = "{yil} öğretim yılı {donem} Dönem'inde 3308 sayılı Mesleki Eğitim Kanunu ve Millî Eğitim Bakanlığı Ortaöğretim Kurumları Yönetmeliği'nin 88 inci maddesi gereğince, {tarih} tarihinden geçerli olmak üzere aşağıda bilgileri verilen işletmelerin koordinatörlük görevleri tarafınıza verilmiştir.";
    public string PrincipalName { get; set; } = "Mustafa KEMAL";
    public string VicePrincipalName { get; set; } = "Hasan YILDIRIM";
    public bool IncludeSaturday { get; set; } = false;
    public int MaxHoursPerTeacher { get; set; } = 16;
    public double HoursPerStudent { get; set; } = 0.5;
    public double HoursPerEnterprise { get; set; } = 2.0;
    public decimal MinimumWage { get; set; } = 28000.00m;
    public decimal GrossMinimumWage { get; set; } = 0m;
    public int MinimumWageYear { get; set; } = DateTime.Now.Year;

    /// <summary>
    /// SGK e-Bildirge PEK hesaplamasında kullanılan brüt asgari ücret.
    /// Eğer GrossMinimumWage tanımlı ise o kullanılır, girilmemişse Net Asgari Ücret / 0.85 olarak otomatik hesaplanır.
    /// </summary>
    public decimal EffectiveGrossMinimumWage => GrossMinimumWage > 0 
        ? GrossMinimumWage 
        : (MinimumWage > 0 ? Math.Round(MinimumWage / 0.85m, 2) : 33000.00m);
    public string OrgunStartDate { get; set; } = $"{DateTime.Now.Year}-09-09";
    public string OrgunEndDate { get; set; } = $"{DateTime.Now.Year + 1}-06-20";
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "admin123";
    public string? LastBackupDate { get; set; }
    public bool TeacherCanEditData { get; set; } = false;

    // Sigorta ve SGK Bilgileri Ayarları
    public string SgkSicilNo { get; set; } = "";
    public string SgkKontrolNo { get; set; } = "90";
    public string SgkAraciNo { get; set; } = "000";
    public string VergiNo { get; set; } = "";
    public string SgkNaceKodu { get; set; } = "";
    public string SgkSonSinifBildirgeKodu { get; set; } = "Son Sınıflar 49/50 Kodunu Kullansın.";
    public string SgkYetkiliAdSoyad { get; set; } = "";
    public string SgkYetkiliUnvan { get; set; } = "";
    public string SgkYetkiliEposta { get; set; } = "";
    public string SgkYetkiliTelefon { get; set; } = "";
    public string SgkBildirimNotu { get; set; } = "3308 Sayılı Kanun kapsamında öğrencilerin İş Kazası ve Meslek Hastalığı sigorta primleri okul müdürlüğümüzce ödenmektedir.";

    // SGK İşe Giriş / Çıkış Parametreleri (Örgün ve MESEM)
    public string OrgunSigortaKolu { get; set; } = "37";
    public string OrgunOgrenimKodu { get; set; } = "3";
    public string OrgunMeslekKodu { get; set; } = "9901.02";
    public string OrgunGorevKodu { get; set; } = "5";

    public string MesemSigortaKolu { get; set; } = "7";
    public string MesemOgrenimKodu { get; set; } = "3";
    public string MesemMeslekKodu { get; set; } = "9901.03";
    public string MesemGorevKodu { get; set; } = "5";

    // Okul İletişim & Adres Ayarları
    public string SchoolAddress { get; set; } = "Atatürk Mah. Cumhuriyet Cad. No:1";
    public string SchoolCity { get; set; } = "İSTANBUL";
    public string SchoolDistrict { get; set; } = "KADIKÖY";

    public string DistrictAndCity => string.IsNullOrWhiteSpace(SchoolDistrict) && string.IsNullOrWhiteSpace(SchoolCity)
        ? "KADIKÖY / İSTANBUL"
        : $"{SchoolDistrict} / {SchoolCity}".Trim(' ', '/');

    // Compatibility Property Aliases
    public string SchoolCode { get => KurumKodu; set => KurumKodu = value; }
    public string DistrictGovernorate { get => Valilik; set => Valilik = value; }
    public string IlceMem { get => DistrictMem; set => DistrictMem = value; }
    public string OfficialLetterNumber { get => SayiNo; set => SayiNo = value; }
    public string LetterDate { get => FormDate; set => FormDate = value; }
    public int MaxWeeklyCoordinatorHours { get => MaxHoursPerTeacher; set => MaxHoursPerTeacher = value; }
    public decimal StatutoryMinimumWage { get => MinimumWage; set => MinimumWage = value; }
    public string AdminUsername { get => Username; set => Username = value; }
    public string AdminPassword { get => Password; set => Password = value; }
}
