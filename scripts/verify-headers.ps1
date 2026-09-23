# Copyright © Erickson Lopez. MIT License.
$targetHeader = "// Copyright " + [char]0x00A9 + " Erickson Lopez. MIT License."
$violations = @()
$checkedCount = 0

$files = Get-ChildItem -Path src, tests, samples -Filter *.cs -Recurse | Where-Object { 
    $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Name -notmatch '\.g\.cs$' -and $_.Name -notmatch '\.AssemblyInfo\.cs$'
}

foreach ($f in $files) {
    $file = $f.FullName
    $checkedCount++
    $lines = [System.IO.File]::ReadAllLines($file, [System.Text.Encoding]::UTF8)
    
    if ($lines.Length -eq 0 -or $lines[0].Trim() -ne $targetHeader) {
        $actual = if ($lines.Length -gt 0) { $lines[0].Trim() } else { "<EMPTY>" }
        $violations += [PSCustomObject]@{ File = $f.FullName; ActualHeader = $actual }
    }
}

if ($violations.Count -gt 0) {
    Write-Error "Found $($violations.Count) files without the standard MIT copyright header '$targetHeader':"
    foreach ($v in $violations) {
        Write-Host "  - $($v.File) (Found: '$($v.ActualHeader)')" -ForegroundColor Red
    }
    exit 1
} else {
    Write-Host "✅ All $checkedCount source files contain the exact copyright header." -ForegroundColor Green
    exit 0
}
