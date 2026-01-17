<#
.SYNOPSIS
    Projedeki kod dosyalarını YÜKSEK PERFORMANSLA ve UTF-8 BOM destekli toplar.
    
.DESCRIPTION
    Türkçe karakter sorununu çözmek için hem okuma hem yazma işleminde UTF-8 zorlanır.
#>

param (
    [string]$OutputFileName = "Codebase.txt",
    [string]$SourcePath = "."
)

# 1. Ayarlar
$includeExtensions = @("*.cs", "*.csproj", "*.json", "*.xml")
$excludeFolders = @("\\bin\\", "\\obj\\", "\\.git\\", "\\.vs\\", "\\Migrations\\", "\\TestResults\\")

Write-Host "Kod üssü taranıyor (Türkçe Karakter Fix)..." -ForegroundColor Cyan

# Dosyaları bul
$files = Get-ChildItem -Path $SourcePath -Recurse -Include $includeExtensions

# Dosya varsa sil
if (Test-Path $OutputFileName) { Remove-Item $OutputFileName }

# --- Encoding Ayarı: UTF-8 with BOM ---
# VS Code ve Windows Notepad'in Türkçe karakterleri kesin tanıması için BOM (Byte Order Mark) ekliyoruz.
$encoding = [System.Text.UTF8Encoding]::new($true) 
$stream = [System.IO.StreamWriter]::new($OutputFileName, $false, $encoding)

try {
    $count = 0
    $totalFiles = $files.Count
    
    foreach ($file in $files) {
        $skip = $false
        foreach ($exclude in $excludeFolders) {
            if ($file.FullName.Contains($exclude)) {
                $skip = $true
                break
            }
        }
        if ($skip) { continue }

        if ($count % 10 -eq 0) {
            Write-Progress -Activity "Exporting Codebase" -Status "Processing: $($file.Name)" -PercentComplete (($count / $totalFiles) * 100)
        }

        # Başlığı Yaz
        $header = "`n" + ("=" * 80) + "`nFILE: " + $file.FullName.Substring($PWD.Path.Length + 1) + "`n" + ("=" * 80) + "`n"
        $stream.WriteLine($header)

        # --- KRİTİK DÜZELTME BURADA ---
        # Kaynak dosyasını okurken de UTF-8 olduğunu varsayıyoruz.
        # Eğer kaynak dosyaların ANSI ise burayı değiştirmen gerekebilir ama modern .NET projeleri %99 UTF-8'dir.
        $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
        
        $stream.WriteLine($content)
        
        $count++
    }
}
finally {
    $stream.Flush()
    $stream.Close()
    $stream.Dispose()
    Write-Progress -Activity "Exporting Codebase" -Completed
}

Write-Host "İşlem Tamamlandı! Türkçe karakter destekli $count dosya yazıldı." -ForegroundColor Green