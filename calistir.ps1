Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  MEB Koordinatörlük Dağıtım ve Ücret Takip Sistemi" -ForegroundColor Yellow
Write-Host "  Saf C# (.NET 9 Windows Forms) ve Yerel SQLite" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Uygulama başlatılıyor, lütfen bekleyin..." -ForegroundColor Green
dotnet run --project "$PSScriptRoot\src\MebKoordinatorluk.UI"
