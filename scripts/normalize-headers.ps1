# Copyright © Erickson Lopez. MIT License.
$targetHeader = "// Copyright © Erickson Lopez. MIT License."
$modifiedCount = 0
$checkedCount = 0

$files = Get-ChildItem -Path src, tests, samples -Filter *.cs -Recurse | Where-Object { 
    $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Name -notmatch '\.g\.cs$' -and $_.Name -notmatch '\.AssemblyInfo\.cs$'
}

foreach ($f in $files) {
    $file = $f.FullName
    $checkedCount++
    $lines = [System.IO.File]::ReadAllLines($file, [System.Text.Encoding]::UTF8)
    
    if ($lines.Length -gt 0) {
        $firstLine = $lines[0].Trim()
        if ($firstLine -ne $targetHeader) {
            if ($firstLine.StartsWith("// Copyright") -or $firstLine.StartsWith("//Copyright")) {
                $lines[0] = $targetHeader
                [System.IO.File]::WriteAllLines($file, $lines, (New-Object System.Text.UTF8Encoding($false)))
                $modifiedCount++
                Write-Host "Updated: $($f.FullName)"
            } else {
                $newLines = @($targetHeader) + $lines
                [System.IO.File]::WriteAllLines($file, $newLines, (New-Object System.Text.UTF8Encoding($false)))
                $modifiedCount++
                Write-Host "Prepended: $($f.FullName)"
            }
        }
    }
}

Write-Host "Checked $checkedCount source files. Normalized $modifiedCount files."
