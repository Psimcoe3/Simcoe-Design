param(
    [switch]$DryRun,
    [switch]$NoKillExisting,
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $PSCommandPath
Set-Location $scriptRoot

$projectPath = Join-Path $scriptRoot "ElectricalComponentSandbox\ElectricalComponentSandbox.csproj"
if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Project file not found: $projectPath"
}

Write-Host "Checking .NET SDK..."
$dotnetVersion = & dotnet --version
if ($LASTEXITCODE -ne 0) {
    throw "dotnet CLI is not available. Install .NET SDK 8+ and retry."
}
Write-Host "Using .NET SDK $dotnetVersion"

if (-not $NoKillExisting) {
    $running = Get-Process -Name "ElectricalComponentSandbox" -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "Stopping existing ElectricalComponentSandbox process(es)..."
        foreach ($proc in $running) {
            try {
                [void]$proc.CloseMainWindow()
            }
            catch {
            }
        }

        Start-Sleep -Milliseconds 900

        $stillRunning = Get-Process -Name "ElectricalComponentSandbox" -ErrorAction SilentlyContinue
        if ($stillRunning) {
            $stillRunning | Stop-Process -Force
        }
    }
}

$args = @("watch")
if ($NoRestore) {
    $args += "--no-restore"
}
$args += @(
    "--project", $projectPath,
    "run",
    "--framework", "net8.0-windows"
)

Write-Host ""
Write-Host "Starting auto rebuild + run loop..."
Write-Host "Command: dotnet $($args -join ' ')"
Write-Host "Press Ctrl+C to stop."
Write-Host ""

if ($DryRun) {
    Write-Host "DryRun enabled. Exiting before launch."
    exit 0
}

& dotnet @args
exit $LASTEXITCODE
