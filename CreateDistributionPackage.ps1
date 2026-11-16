# MEB Koordinatörlük Dağıtım Paketi Hazırlama Betiği
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
if (-not $root) { $root = Get-Location }

$distDir = Join-Path $root "Dagitim_Paketi"
if (Test-Path $distDir) {
    Remove-Item -Path $distDir -Recurse -Force
}
New-Item -ItemType Directory -Path $distDir | Out-Null

$dbSource = Join-Path $root "src\MebKoordinatorluk.UI\bin\Debug\net9.0-windows\Data\meb_koordinatorluk.db"

Write-Host "1. Bağımsız Klasör Paketi Derleniyor (Self-Contained)..." -ForegroundColor Cyan
$scDir = Join-Path $distDir "MebKoordinatorluk_Bagimsiz_x64"
dotnet publish "$root\src\MebKoordinatorluk.UI\MebKoordinatorluk.UI.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $scDir

# PDB dosyalarını temizle (son kullanıcıya gereksiz)
Get-ChildItem -Path $scDir -Filter "*.pdb" | Remove-Item -Force

# Veritabanını aktar
$scDataDir = Join-Path $scDir "Data"
if (-not (Test-Path $scDataDir)) { New-Item -ItemType Directory -Path $scDataDir | Out-Null }
if (Test-Path $dbSource) {
    Copy-Item $dbSource (Join-Path $scDataDir "meb_koordinatorluk.db") -Force
}

# Başlatıcı ve Kısayol bat'leri ekle
$batContent = @"
@echo off
start "" "%~dp0MebKoordinatorluk.UI.exe"
exit
"@
Set-Content -Path (Join-Path $scDir "Programi_Baslat.bat") -Value $batContent -Encoding UTF8

$shortcutBat = @"
@echo off
set SCRIPT="%TEMP%\%RANDOM%-%RANDOM%.vbs"
echo Set oWS = WScript.CreateObject("WScript.Shell") >> %SCRIPT%
echo sLinkFile = oWS.SpecialFolders("Desktop") ^& "\MEB Koordinatorluk.lnk" >> %SCRIPT%
echo Set oLink = oWS.CreateShortcut(sLinkFile) >> %SCRIPT%
echo oLink.TargetPath = "%~dp0MebKoordinatorluk.UI.exe" >> %SCRIPT%
echo oLink.WorkingDirectory = "%~dp0" >> %SCRIPT%
echo oLink.Description = "MEB Koordinatorluk Modulu" >> %SCRIPT%
echo oLink.Save >> %SCRIPT%
cscript /nologo %SCRIPT%
del %SCRIPT%
echo Masaustune kisayol basariyla olusturuldu!
pause
"@
Set-Content -Path (Join-Path $scDir "Masaustune_Kisayol_Olustur.bat") -Value $shortcutBat -Encoding OEM

$scBeniOku = @"
========================================================================
    MEB KOORDINATORLUK VE STAJ YONETIM SISTEMI (BAGIMSIZ SURUM)
========================================================================

Bu paket "Self-Contained" (Kendi Kendine Yeten) olarak hazirlanmistir.
Hedef bilgisayarda .NET 9 veya baska herhangi bir program kurulu olmak 
zorunda DEGILDIR.

NASIL CALISTIRILIR?
1. Bu klasoru bilgisayarinizda istediginiz bir yere (ornegin C:\MEB_Koordinatorluk
   veya Belgelerim klasorune) kopyalayiniz.
2. Klasor icindeki "Programi_Baslat.bat" veya "MebKoordinatorluk.UI.exe"
   dosyasina cift tiklayarak programi baslatabilirsiniz.
3. Dilerseniz "Masaustune_Kisayol_Olustur.bat" dosyasina cift tiklayarak
   masaustunuze otomatik simge ekleyebilirsiniz.

VERILER VE YEDEKLEME:
- Tum verileriniz bu klasorun icindeki "Data\meb_koordinatorluk.db" dosyasinda saklanir.
- Programi baska bilgisayara tasirken sadece bu klasoru kopyalamaniz veya
  "Data\meb_koordinatorluk.db" dosyasini yedeklemeniz yeterlidir.
"@
Set-Content -Path (Join-Path $scDir "BeniOku.txt") -Value $scBeniOku -Encoding UTF8


Write-Host "2. Tek Dosya Paketi Derleniyor (Single-File Exe)..." -ForegroundColor Cyan
$sfDir = Join-Path $distDir "MebKoordinatorluk_TekDosya_x64"
dotnet publish "$root\src\MebKoordinatorluk.UI\MebKoordinatorluk.UI.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $sfDir

Get-ChildItem -Path $sfDir -Filter "*.pdb" | Remove-Item -Force
$sfDataDir = Join-Path $sfDir "Data"
if (-not (Test-Path $sfDataDir)) { New-Item -ItemType Directory -Path $sfDataDir | Out-Null }
if (Test-Path $dbSource) {
    Copy-Item $dbSource (Join-Path $sfDataDir "meb_koordinatorluk.db") -Force
}
Set-Content -Path (Join-Path $sfDir "Programi_Baslat.bat") -Value $batContent -Encoding UTF8
Set-Content -Path (Join-Path $sfDir "Masaustune_Kisayol_Olustur.bat") -Value $shortcutBat -Encoding OEM
Set-Content -Path (Join-Path $sfDir "BeniOku.txt") -Value $scBeniOku -Encoding UTF8


Write-Host "3. Hafif Paket Derleniyor (Framework-Dependent)..." -ForegroundColor Cyan
$fdDir = Join-Path $distDir "MebKoordinatorluk_Hafif_x64"
dotnet publish "$root\src\MebKoordinatorluk.UI\MebKoordinatorluk.UI.csproj" -c Release -r win-x64 --self-contained false -o $fdDir

Get-ChildItem -Path $fdDir -Filter "*.pdb" | Remove-Item -Force
$fdDataDir = Join-Path $fdDir "Data"
if (-not (Test-Path $fdDataDir)) { New-Item -ItemType Directory -Path $fdDataDir | Out-Null }
if (Test-Path $dbSource) {
    Copy-Item $dbSource (Join-Path $fdDataDir "meb_koordinatorluk.db") -Force
}
Set-Content -Path (Join-Path $fdDir "Programi_Baslat.bat") -Value $batContent -Encoding UTF8
Set-Content -Path (Join-Path $fdDir "Masaustune_Kisayol_Olustur.bat") -Value $shortcutBat -Encoding OEM

$fdBeniOku = @"
========================================================================
    MEB KOORDINATORLUK VE STAJ YONETIM SISTEMI (HAFIF SURUM - 4 MB)
========================================================================

Bu paket cok kucuk boyutlu (yaklasik 4 MB) bir dagitim paketidir.
NOT: Calisabilmesi icin hedef bilgisayarda ".NET 9.0 Desktop Runtime (x64)" 
kurulu olmalidir. Eger kurulu degilse, Bagimsiz paketi (MebKoordinatorluk_Bagimsiz_x64) 
kullaniniz.

GEREKLI RUNTIME INDIRME LINKI:
https://dotnet.microsoft.com/download/dotnet/9.0
(".NET Desktop Runtime - Windows x64" seciniz)
"@
Set-Content -Path (Join-Path $fdDir "BeniOku.txt") -Value $fdBeniOku -Encoding UTF8


Write-Host "4. ZIP Arşivleri Oluşturuluyor..." -ForegroundColor Cyan
Compress-Archive -Path "$scDir\*" -DestinationPath (Join-Path $distDir "MebKoordinatorluk_Bagimsiz_x64.zip") -Force
Compress-Archive -Path "$sfDir\*" -DestinationPath (Join-Path $distDir "MebKoordinatorluk_TekDosya_x64.zip") -Force
Compress-Archive -Path "$fdDir\*" -DestinationPath (Join-Path $distDir "MebKoordinatorluk_Hafif_x64.zip") -Force


$anaRehber = @"
==================================================================================
           MEB KOORDINATORLUK VE STAJ YONETIM SISTEMI - DAGITIM PAKETI
==================================================================================

Bu klasorde programi baska bilgisayarlara tasimaniz ve kullanmaniz icin 3 farkli 
secenek hazirlanmistir:

1. MebKoordinatorluk_Bagimsiz_x64 (VE ZIP ARSIVI) *** EN COK TAVSIYE EDILEN ***
   - Hedef bilgisayarda hicbir sey (.NET 9 vb.) kurulu olmasi GEREKMEZ.
   - Dogrudan cift tikla calisir.
   - Tum ogrencileriniz, ogretmenleriniz ve veritabaniniz icinde hazir yer alir.
   - Flash bellege atip istediginiz bilgisayarda calistirabilirsiniz.

2. MebKoordinatorluk_TekDosya_x64 (VE ZIP ARSIVI)
   - Sadece tek bir EXE dosyasindan ve Data klasorunden olusur.
   - Yine .NET 9 gerektirmez.

3. MebKoordinatorluk_Hafif_x64 (VE ZIP ARSIVI)
   - Sadece 4 MB boyutundadir.
   - Calismasi icin bilgisayarda Microsoft .NET Desktop Runtime 9.0 kurulu olmalidir.

YENI BILGISAYARA TASIMA ADIMLARI:
1. "MebKoordinatorluk_Bagimsiz_x64.zip" dosyasini bir flash bellege kopyalayiniz.
2. Diger bilgisayara gecip zip dosyasina sag tiklayip "Tumunu Ayikla..." (Extract All) deyiniz.
3. Olusan klasorun icindeki "Masaustune_Kisayol_Olustur.bat" dosyasina cift tiklayiniz.
4. Masaustunuze gelen simgeye tiklayarak calistiriniz!
==================================================================================
"@
Set-Content -Path (Join-Path $distDir "KULLANIM_VE_TASIMA_REHBERI.txt") -Value $anaRehber -Encoding UTF8

Write-Host "Tüm paketler başarıyla hazırlandı! Konum: $distDir" -ForegroundColor Green
