param(
    [string[]]$Paths = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$crlfExtensions = @(
    ".cs", ".xaml", ".csproj", ".props", ".targets",
    ".sln", ".slnx", ".config", ".ps1", ".bat", ".cmd"
)
$lfExtensions = @(".sh")

if ($Paths.Count -eq 0) {
    $pathsFromGit = @(
        git diff --name-only --diff-filter=ACMRTUXB
        git ls-files --others --exclude-standard
    )
    $Paths = @(
        $pathsFromGit |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique
    )
}

if ($Paths.Count -eq 0) {
    Write-Host "No changed files to normalize."
    exit 0
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$normalizedCount = 0

foreach ($path in $Paths) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        continue
    }

    $ext = [System.IO.Path]::GetExtension($path).ToLowerInvariant()
    $targetEol = $null
    if ($crlfExtensions -contains $ext) {
        $targetEol = "`r`n"
    }
    elseif ($lfExtensions -contains $ext) {
        $targetEol = "`n"
    }
    else {
        continue
    }

    $original = [System.IO.File]::ReadAllText($path)
    $rewritten = [System.Text.RegularExpressions.Regex]::Replace($original, "\r\n|\n|\r", $targetEol)
    if ($rewritten -ne $original) {
        [System.IO.File]::WriteAllText($path, $rewritten, $utf8NoBom)
        $normalizedCount++
        Write-Host "Normalized: $path"
    }
}

Write-Host "Normalization complete. Files changed: $normalizedCount"
