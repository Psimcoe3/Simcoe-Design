param()

$expandedScript = Join-Path $PSScriptRoot 'ui-smoke-expanded.ps1'
if (-not (Test-Path $expandedScript)) {
  Write-Error "Missing script: $expandedScript"
  exit 1
}

& pwsh -NoProfile -ExecutionPolicy Bypass -File $expandedScript
exit $LASTEXITCODE
