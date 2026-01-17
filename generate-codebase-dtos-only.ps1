# =============================================================================
# FILE: Export-ModuleCode.ps1
# DESCRIPTION: Belirtilen desenlere uyan dosyaları bulur ve tek bir txt dosyasında birleştirir.
# AUTHOR: Gemini & User
# =============================================================================

# 1. AYARLAR
# -----------------------------------------------------------------------------
# Çıktı dosyasının adı
$OutputFileName = "ModuleSourceCode.txt"

# Aranacak dosya desenleri (Senin istediklerin)
$IncludePatterns = @("*Dto.cs", "*Dtos.cs", "*Command.cs", "*Result.cs", "*Request.cs", "*Response.cs") 
# Not: *Query.cs'i de ekledim, genelde Command ile beraber o da lazım olur, istersen silebilirsin.

# Aramanın yapılacağı kök dizin (Scriptin çalıştığı klasör)
$SourcePath = Get-Location

# 2. İŞLEM BAŞLIYOR
# -----------------------------------------------------------------------------
Write-Host "Tarama başlatılıyor: $SourcePath" -ForegroundColor Cyan
Write-Host "Aranan desenler: $($IncludePatterns -join ', ')" -ForegroundColor Yellow

# Eğer eski çıktı dosyası varsa sil
if (Test-Path $OutputFileName) {
    Remove-Item $OutputFileName
    Write-Host "Eski '$OutputFileName' dosyası silindi." -ForegroundColor Gray
}

# Dosyaları bul (Alt klasörler dahil -Recurse)
$Files = Get-ChildItem -Path $SourcePath -Recurse -Include $IncludePatterns | Where-Object { $_.Name -ne $OutputFileName }

if ($Files.Count -eq 0) {
    Write-Host "HATA: Belirtilen kriterlere uygun hiç dosya bulunamadı!" -ForegroundColor Red
    exit
}

Write-Host "$($Files.Count) adet dosya bulundu. Birleştiriliyor..." -ForegroundColor Green

# 3. DOSYALARI BİRLEŞTİRME
# -----------------------------------------------------------------------------
foreach ($File in $Files) {
    # Dosyanın tam içeriğini oku
    $Content = Get-Content -Path $File.FullName -Raw -Encoding UTF8

    # Başlık formatı (Yapay zekanın dosya ismini net anlaması için)
    $Header = "`n================================================================================`n" +
              "FILE: $($File.Name)`n" +
              "PATH: $($File.FullName.Replace($SourcePath.Path, '').Trim('\'))`n" +
              "================================================================================`n"

    # Çıktı dosyasına ekle
    Add-Content -Path $OutputFileName -Value $Header -Encoding UTF8
    Add-Content -Path $OutputFileName -Value $Content -Encoding UTF8
}

# 4. BİTİŞ
# -----------------------------------------------------------------------------
Write-Host "İşlem Tamamlandı!" -ForegroundColor Cyan
Write-Host "Dosya oluşturuldu: $(Join-Path $SourcePath $OutputFileName)" -ForegroundColor Green