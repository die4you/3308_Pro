# 3308 Pro - Inno Setup Kurulum Paketi Üretim Betiği
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$root = $PSScriptRoot
if (-not $root) { $root = "C:\Users\trgy\.gemini\antigravity\scratch\MebKoordinatorluk" }

$distDir = Join-Path $root "Dagitim_Paketi"
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir | Out-Null
}

$scDir = Join-Path $distDir "3308_Pro_Bagimsiz_x64"
Write-Host "1. 3308 Pro Derleniyor ve Yayımlanıyor (Self-Contained x64)..." -ForegroundColor Cyan
dotnet publish "$root\src\MebKoordinatorluk.UI\MebKoordinatorluk.UI.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -m:1 /p:nodeReuse=false -o $scDir

Write-Host "2. PDB dosyaları temizleniyor..." -ForegroundColor Cyan
Get-ChildItem -Path $scDir -Filter "*.pdb" | Remove-Item -Force -ErrorAction SilentlyContinue

# app.ico kopyala
$icoSource = Join-Path $root "src\MebKoordinatorluk.UI\app.ico"
if (Test-Path $icoSource) {
    Copy-Item $icoSource (Join-Path $scDir "app.ico") -Force
}

# Data klasörü (Sıfır / temiz başlangıç veritabanı şablonu)
$scDataDir = Join-Path $scDir "Data"
if (-not (Test-Path $scDataDir)) { New-Item -ItemType Directory -Path $scDataDir | Out-Null }
Get-ChildItem -Path $scDataDir -Filter "*.db*" | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "3. Inno Setup Kurulum Sihirbazı Betiği Hazırlanıyor..." -ForegroundColor Cyan
$setupDir = Join-Path $distDir "Kurulum_Sihirbazi"
if (-not (Test-Path $setupDir)) { New-Item -ItemType Directory -Path $setupDir | Out-Null }

if (Test-Path $icoSource) {
    Copy-Item $icoSource (Join-Path $setupDir "app.ico") -Force
}

$issContent = @"
; 3308 Pro - MEB Koordinatörlük ve Beceri Eğitimi Sistemi
; Inno Setup 6 Kurulum Betiği

#define MyAppName "3308 Pro"
#define MyAppVersion "2.9"
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
OutputBaseFilename=3308_Pro_Kurulum_v2.9
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
$issPath = Join-Path $setupDir "3308_Pro_Setup.iss"
Set-Content -Path $issPath -Value $issContent -Encoding UTF8

Write-Host "4. Inno Setup Derleyicisi (ISCC.exe) Çalıştırılıyor..." -ForegroundColor Cyan
$isccPaths = @(
    "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LocalAppData\Programs\Inno Setup 6\ISCC.exe"
)
$isccExe = $isccPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $isccExe) {
    throw "Inno Setup derleyicisi (ISCC.exe) sistemde bulunamadı!"
}

Write-Host "Inno Setup bulundu: $isccExe" -ForegroundColor Green
& $isccExe $issPath

$outputInstaller = Join-Path $distDir "3308_Pro_Kurulum_v2.9.exe"
if (Test-Path $outputInstaller) {
    $item = Get-Item $outputInstaller
    $sizeMb = [math]::Round($item.Length / 1MB, 2)
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "  INNO SETUP KURULUM PAKETİ BAŞARIYLA ÜRETİLDİ!" -ForegroundColor Green
    Write-Host "  Dosya: $($item.FullName)" -ForegroundColor Green
    Write-Host "  Boyut: $sizeMb MB ($($item.Length) bayt)" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
} else {
    throw "Kurulum dosyası üretilemedi: $outputInstaller"
}
