# Copyright © Erickson Lopez. MIT License.
$files = Get-ChildItem -Path . -Filter *.md -Recurse | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj|\.git|node_modules)[\\/]'
}

$brokenLinks = @()
$totalLinks = 0

foreach ($f in $files) {
    $content = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
    $dir = $f.DirectoryName

    # Match markdown links: [text](target)
    $matches = [regex]::Matches($content, '\[([^\]]+)\]\(([^)]+)\)')

    foreach ($m in $matches) {
        $linkTarget = $m.Groups[2].Value.Trim()
        
        # Skip external web URLs and mailto
        if ($linkTarget.StartsWith("http://") -or $linkTarget.StartsWith("https://") -or $linkTarget.StartsWith("mailto:")) {
            continue
        }

        # Skip anchor-only links (#section)
        if ($linkTarget.StartsWith("#")) {
            continue
        }

        $totalLinks++
        
        # Split target file from anchor (#)
        $targetFilePart = ($linkTarget -split '#')[0]
        if ([string]::IsNullOrWhiteSpace($targetFilePart)) {
            continue
        }

        # Resolve path relative to current markdown file
        $resolvedPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($dir, $targetFilePart))
        
        # Check if file exists or directory exists
        if (-not (Test-Path $resolvedPath)) {
            $brokenLinks += [PSCustomObject]@{
                SourceFile = $f.FullName.Substring((Get-Location).Path.Length).TrimStart('\', '/')
                LinkText = $m.Groups[1].Value
                Target = $linkTarget
                ResolvedPath = $resolvedPath
                Reason = "Target does not exist"
            }
        } else {
            # Cross-platform case-sensitivity verification (prevents Linux CI failures)
            $relSegments = $targetFilePart.Replace('/', '\').Split('\', [System.StringSplitOptions]::RemoveEmptyEntries)
            $curr = $dir
            foreach ($seg in $relSegments) {
                if ($seg -eq '.') { continue }
                if ($seg -eq '..') {
                    $curr = Split-Path $curr -Parent
                    continue
                }
                $exactMatch = Get-ChildItem -LiteralPath $curr | Where-Object { $_.Name -ceq $seg }
                if (-not $exactMatch) {
                    $actualEntry = Get-ChildItem -LiteralPath $curr | Where-Object { $_.Name -ieq $seg }
                    $actualName = if ($actualEntry) { $actualEntry.Name } else { "unknown" }
                    $brokenLinks += [PSCustomObject]@{
                        SourceFile = $f.FullName.Substring((Get-Location).Path.Length).TrimStart('\', '/')
                        LinkText = $m.Groups[1].Value
                        Target = $linkTarget
                        ResolvedPath = $resolvedPath
                        Reason = "Casing mismatch: '$seg' vs actual disk entry '$actualName'"
                    }
                    break
                }
                $curr = [System.IO.Path]::Combine($curr, $seg)
            }
        }
    }
}

if ($brokenLinks.Count -gt 0) {
    Write-Error "Found $($brokenLinks.Count) broken internal links:"
    foreach ($b in $brokenLinks) {
        Write-Host "  - in $($b.SourceFile): [$($b.LinkText)]($($b.Target)) -> $($b.Reason) ('$($b.ResolvedPath)')" -ForegroundColor Red
    }
    exit 1
} else {
    Write-Host "✅ All $totalLinks internal links validated successfully." -ForegroundColor Green
    exit 0
}
