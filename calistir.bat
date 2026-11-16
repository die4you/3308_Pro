@echo off
chcp 65001 >nul
title MEB Koordinatörlük Dağıtım ve Ücret Takip Sistemi
echo ========================================================
echo   MEB Koordinatörlük Dağıtım ve Ücret Takip Sistemi
echo   Saf C# (.NET 9 Windows Forms) ve Yerel SQLite
echo ========================================================
echo.
echo Uygulama başlatılıyor, lütfen bekleyin...
cd /d "%~dp0"
dotnet run --project src\MebKoordinatorluk.UI
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Bir hata oluştu! Çıkmak için bir tuşa basın...
    pause >nul
)
