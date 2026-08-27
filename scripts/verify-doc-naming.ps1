# Copyright © Erickson Lopez. MIT License.
$allowedExceptions = @(
    "README.md",
    "CHANGELOG.md",
    "CODE_OF_CONDUCT.md",
    "CONTRIBUTING.md",
    "SECURITY.md",
    "SUPPORT.md",
    "PULL_REQUEST_TEMPLATE.md"
)

$violations = @()
$checkedCount = 0

$files = Get-ChildItem -Path . -Filter *.md -Recurse | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj|\.git|node_modules)[\\/]'
}

foreach ($f in $files) {
    $checkedCount++
    $fileName = $f.Name
    $relPath = $f.FullName.Substring((Get-Location).Path.Length).TrimStart('\', '/')
    
    # Check if in standard root exceptions or issue templates
    if ($allowedExceptions -contains $fileName -or $relPath -match '^\.github[\\/]ISSUE_TEMPLATE[\\/]') {
        continue
    }

    # Must be lowercase kebab-case (letters, numbers, hyphens only, ending in .md)
    if ($fileName -notmatch '^[a-z0-9]+(-[a-z0-9]+)*\.md$') {
        $violations += [PSCustomObject]@{ File = $relPath; Name = $fileName }
    }
}

if ($violations.Count -gt 0) {
    Write-Error "Found $($violations.Count) markdown documentation files not conforming to kebab-case:"
    foreach ($v in $violations) {
        Write-Host "  - $($v.File) ($($v.Name))" -ForegroundColor Red
    }
    exit 1
} else {
    Write-Host "✅ All $checkedCount markdown files conform to kebab-case conventions." -ForegroundColor Green
    exit 0
}
