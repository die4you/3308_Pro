# 3308 Pro - MEB Koordinatörlük Sistemi Dağıtım Paketi Üretim Betiği
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$root = $PSScriptRoot
if (-not $root) { $root = Get-Location }

$distDir = Join-Path $root "Dagitim_Paketi"
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir | Out-Null
}

$scDir = Join-Path $distDir "3308_Pro_Bagimsiz_x64"
$zipPath = Join-Path $distDir "3308_Pro_v3.0_Portable.zip"

Write-Host "1. 3308 Pro Bağımsız Dağıtım Paketi Derleniyor (Self-Contained x64)..." -ForegroundColor Cyan
dotnet publish "$root\src\MebKoordinatorluk.UI\MebKoordinatorluk.UI.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -m:1 /p:nodeReuse=false -o $scDir

Write-Host "2. PDB hata ayıklama dosyaları temizleniyor..." -ForegroundColor Cyan
Get-ChildItem -Path $scDir -Filter "*.pdb" | Remove-Item -Force

# İkonu kopyala
$icoSource = Join-Path $root "src\MebKoordinatorluk.UI\app.ico"
if (Test-Path $icoSource) {
    Copy-Item $icoSource (Join-Path $scDir "app.ico") -Force
}

Write-Host "3. Veritabanı şablonu hazırlanıyor (Temiz / Sıfır Kayıt)..." -ForegroundColor Cyan
$scDataDir = Join-Path $scDir "Data"
if (-not (Test-Path $scDataDir)) { New-Item -ItemType Directory -Path $scDataDir | Out-Null }

# Dağıtım paketinde eski geliştirme/test veritabanı varsa kaldırılır:
$existingPkgDb = Join-Path $scDataDir "meb_koordinatorluk.db"
if (Test-Path $existingPkgDb) {
    Remove-Item -Path $existingPkgDb -Force
}
# NOT: Dağıtım paketi veritabanı olmadan dağıtılır. Program ilk açılışta
# DatabaseInitializer aracılığıyla 0 kayıtlı, temiz veritabanını otomatik üretir.
# Okullar sisteme kendi öğrenci, öğretmen ve işletmelerini girecektir.

Write-Host "4. Başlatıcı ve masaüstü kısayol betikleri ekleniyor..." -ForegroundColor Cyan
$batContent = @"
@echo off
start "" "%~dp03308_Pro.exe"
exit
"@
Set-Content -Path (Join-Path $scDir "Programi_Baslat.bat") -Value $batContent -Encoding UTF8

$shortcutBat = @"
@echo off
set SCRIPT="%TEMP%\%RANDOM%-%RANDOM%.vbs"
echo Set oWS = WScript.CreateObject("WScript.Shell") >> %SCRIPT%
echo sLinkFile = oWS.SpecialFolders("Desktop") ^& "\3308 Pro.lnk" >> %SCRIPT%
echo Set oLink = oWS.CreateShortcut(sLinkFile) >> %SCRIPT%
echo oLink.TargetPath = "%~dp03308_Pro.exe" >> %SCRIPT%
echo oLink.WorkingDirectory = "%~dp0" >> %SCRIPT%
echo oLink.Description = "3308 Pro - MEB Koordinatorluk ve Beceri Egitimi Sistemi" >> %SCRIPT%
if exist "%~dp0app.ico" (
    echo oLink.IconLocation = "%~dp0app.ico,0" >> %SCRIPT%
)
echo oLink.Save >> %SCRIPT%
cscript /nologo %SCRIPT%
del %SCRIPT%
echo Masaustune 3308 Pro kisayolu basariyla olusturuldu!
pause
"@
Set-Content -Path (Join-Path $scDir "Masaustune_Kisayol_Olustur.bat") -Value $shortcutBat -Encoding OEM

$scBeniOku = @"
========================================================================
    3308 PRO - ISLETMELERDE MESLEKI EGITIM VE KOORDINATORLUK SISTEMI
    Surum: 3.0 (Bagimsiz / Portable Surum)
========================================================================

Bu paket "Self-Contained" (Kendi Kendine Yeten) olarak hazirlanmistir.
Hedef bilgisayarda .NET 9 veya baska herhangi bir ek yazilim/runtime kurulu 
olmak zorunda DEGILDIR.

NASIL CALISTIRILIR?
1. Bu klasoru hedef bilgisayarda istediginiz bir yere (ornegin C:\3308_Pro
   veya Belgelerim klasorune) kopyalayiniz.
2. Klasor icindeki "3308_Pro.exe" veya "Programi_Baslat.bat" dosyasina 
   cift tiklayarak baslatabilirsiniz.
3. Dilerseniz "Masaustune_Kisayol_Olustur.bat" dosyasina cift tiklayarak
   masaustunuze tek tikla "3308 Pro" logolu kisayol olusturabilirsiniz.

VERILER VE YEDEKLEME:
- Tum verileriniz bu klasorun icindeki "Data\meb_koordinatorluk.db" dosyasinda saklanir.
- Programi baska bir bilgisayara tasirken sadece bu klasoru kopyalamaniz veya
   "Data\meb_koordinatorluk.db" dosyasini yedeklemeniz yeterlidir.

LISANS:
- Program lisanssiz iken demo modunda (maksimum 5 ogrenci ve 3 isletme) calisir.
- Sinirsiz kullanim icin sag ustteki rozete tiklayip Makine Kodunu ileterek
  lisans anahtarinizi giriniz.
"@
Set-Content -Path (Join-Path $scDir "BeniOku.txt") -Value $scBeniOku -Encoding UTF8

Write-Host "5. ZIP Taşınabilir Arşivi oluşturuluyor..." -ForegroundColor Cyan
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $zipPath) { Remove-Item -Path $zipPath -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory($scDir, $zipPath)

Write-Host "6. Geliştirici Lisans Üreteci Derleniyor ve Paketleniyor..." -ForegroundColor Cyan
$keygenProj = Join-Path $root "src\MebKoordinatorluk.Keygen\MebKoordinatorluk.Keygen.csproj"
$keygenBin = Join-Path $root "bin\LisansUreteci"
if (Test-Path $keygenProj) {
    dotnet publish $keygenProj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -m:1 /p:nodeReuse=false -o $keygenBin
}
$devKeygenDir = Join-Path $distDir "Gelistirici_Lisans_Ureteci"
if (-not (Test-Path $devKeygenDir)) { New-Item -ItemType Directory -Path $devKeygenDir | Out-Null }
if (Test-Path $keygenBin) {
    Copy-Item -Path (Join-Path $keygenBin "*") -Destination $devKeygenDir -Recurse -Force
}

$keygenNote = @"
========================================================================
    3308 PRO - LISANS ANAHTARI URETIM ARACI (GELISTIRICI OZEL)
========================================================================
DIKKAT: BU KLASORU OKULLARA VEYA MUSTERILERE ASLA GONDERMEYINIZ!
Bu arac sadece lisans anahtari uretmek ve satisi kayit altina almak
icin TRGY CYMZ tarafindan kullanilir.

NASIL KULLANILIR?
1. "3308_Pro_Lisans_Ureteci.exe" uygulamasini calistirin.
2. Okuldan gelen Makine Kodunu (HWID) girin.
3. Okul adini, il/ilce bilgisini ve lisans suresini secin.
4. "Lisans Anahtari Uret ve Google E-Tabloya Kaydet" butonuna basin.
5. Uretilen anahtari kopyalayip okula iletin.
6. Kayit otomatik olarak Google E-Tablonuzun "Uretilen_Lisanslar" sekmesine islenecektir.
"@
Set-Content -Path (Join-Path $devKeygenDir "DIKKAT_GELISTIRICIYE_OZEL.txt") -Value $keygenNote -Encoding UTF8

Write-Host "7. Inno Setup Kurulum Sihirbazı Dosyaları Hazırlanıyor..." -ForegroundColor Cyan
$setupDir = Join-Path $distDir "Kurulum_Sihirbazi"
if (-not (Test-Path $setupDir)) { New-Item -ItemType Directory -Path $setupDir | Out-Null }

if (Test-Path $icoSource) {
    Copy-Item $icoSource (Join-Path $setupDir "app.ico") -Force
}

$issContent = @"
; 3308 Pro - MEB Koordinatörlük ve Beceri Eğitimi Sistemi
; Inno Setup 6 Kurulum Betiği

#define MyAppName "3308 Pro"
#define MyAppVersion "3.0"
#define MyAppPublisher "TRGY CYMZ"
#define MyAppExeName "3308_Pro.exe"

[Setup]
AppId={{C6D27A7B-9023-45C6-8968-0740E5E0B33A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName=C:\3308_Pro
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\
OutputBaseFilename=3308_Pro_Kurulum_v3.0
SetupIconFile=app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableDirPage=no
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\3308_Pro_Bagimsiz_x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
Name: "{app}\Data"; Permissions: users-modify

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{group}\{#MyAppName} Veri Klasörü"; Filename: "{app}\Data"
Name: "{group}\Kaldır {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
"@
Set-Content -Path (Join-Path $setupDir "3308_Pro_Setup.iss") -Value $issContent -Encoding UTF8

$compileBat = @"
@echo off
chcp 65001 >nul
echo ========================================================
echo   3308 Pro - Windows Setup Kurulum Derleyicisi
echo ========================================================
echo.

set ISCC=""
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set ISCC="%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set ISCC="%ProgramFiles%\Inno Setup 6\ISCC.exe"
if exist "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" set ISCC="%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"

if %ISCC%=="" (
    echo [UYARI] Inno Setup derleyicisi (ISCC.exe) sistemde bulunamadi!
    echo Setup dosyasini olusturabilmek icin ucretsiz Inno Setup programini indiriniz:
    echo https://jrsoftware.org/isdl.php
    echo.
    echo Inno Setup yuklendikten sonra bu dosyaya tekrar cift tiklayiniz.
    echo.
    pause
    exit /b 1
)

echo Inno Setup bulundu: %ISCC%
echo 3308_Pro_Kurulum_v3.0.exe derleniyor, lutfen bekleyiniz...
echo.

%ISCC% "%~dp03308_Pro_Setup.iss"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [BASARILI] Kurulum dosyasi hazirlandi:
    echo Dagitim_Paketi\3308_Pro_Kurulum_v3.0.exe
    echo.
) else (
    echo.
    echo [HATA] Kurulum derlenirken bir sorun olustu.
    echo.
)
pause
"@
Set-Content -Path (Join-Path $setupDir "Kurulumu_Derle.bat") -Value $compileBat -Encoding OEM

$globalBeniOku = @"
================================================================================
       3308 PRO - RESMİ DAĞITIM VE LİSANSLAMA YÖNETİM PAKETİ (v3.0)
================================================================================

Bu klasör, 3308 Pro yazılımının okullara dağıtımı ve lisans takibi için 
eksiksiz olarak hazırlanmış tüm bileşenleri içerir.

📁 KLASÖR İÇERİĞİ VE KULLANIM REHBERİ:

1. [3308_Pro_Bagimsiz_x64/]
   - Hedef bilgisayara hiçbir şey yüklemeden doğrudan çalışan bağımsız sürümdür.
   - .NET 9 veya harici kütüphane gerektirmez.

2. [3308_Pro_v3.0_Portable.zip]
   - Okullara e-posta, WhatsApp veya USB bellek ile tek parça göndermek için 
     hazırlanmış sıkıştırılmış arşiv dosyasıdır.

3. [Kurulum_Sihirbazi/]
   - Tek tıkla kurulan "3308_Pro_Kurulum_v3.0.exe" Windows Setup dosyasını üretmek 
     için gerekli Inno Setup betiğini ve "Kurulumu_Derle.bat" dosyasını içerir.
   - Bilgisayarınızda Inno Setup kurulu ise "Kurulumu_Derle.bat"a çift tıklamanız 
     Setup.exe dosyasını otomatik üretecektir.

4. [Gelistirici_Lisans_Ureteci/]  (ÖZEL - OKULLARA VERMEYİNİZ!)
   - Sadece sizin kullanacağınız RSA-2048 Lisans Üretim Aracıdır.
   - Okuldan gelen Makine Koduna (HWID) karşılık süreli lisans anahtarı üretir.
   - Üretilen lisansı Google Drive'daki E-Tablonuza ("Uretilen_Lisanslar") 
     otomatik olarak anlık kaydeder.

5. [ONLINE CANLI TAKİP]:
   - Google Drive -> "0-trgycymz / programlar / 3308 Koordinatörlük Dağıtım"
   - "Aktif_Yazilimlar" sayfasından programı kullanan tüm okulların durumunu, 
     kalan günlerini ve aktiflik zamanlarını canlı izleyebilirsiniz.
================================================================================
"@
Set-Content -Path (Join-Path $distDir "BENI_OKU_DAGITIM_REHBERI.txt") -Value $globalBeniOku -Encoding UTF8

Write-Host "8. 3308 Pro Standalone Windows Kurulum Sihirbazı (Setup.exe) Derleniyor..." -ForegroundColor Cyan
$installerProj = Join-Path $root "src\MebKoordinatorluk.Installer\MebKoordinatorluk.Installer.csproj"
if (Test-Path $installerProj) {
    dotnet publish $installerProj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -m:1 /p:nodeReuse=false -o $distDir
}

Write-Host "Tüm dağıtım paketi başarıyla tamamlandı!" -ForegroundColor Green
