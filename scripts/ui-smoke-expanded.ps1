param()

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms

$signature = @"
using System;
using System.Runtime.InteropServices;
public static class NativeInput {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
  public const uint MOUSEEVENTF_LEFTUP = 0x0004;
}
"@
Add-Type -TypeDefinition $signature -ErrorAction SilentlyContinue | Out-Null

function Click-ScreenPoint {
  param([int]$X,[int]$Y,[switch]$Double)
  [NativeInput]::SetCursorPos($X, $Y) | Out-Null
  Start-Sleep -Milliseconds 70
  [NativeInput]::mouse_event([NativeInput]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
  [NativeInput]::mouse_event([NativeInput]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
  if ($Double) {
    Start-Sleep -Milliseconds 110
    [NativeInput]::mouse_event([NativeInput]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    [NativeInput]::mouse_event([NativeInput]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
  }
}

function Drag-ScreenPoint {
  param([int]$X1,[int]$Y1,[int]$X2,[int]$Y2)
  [NativeInput]::SetCursorPos($X1, $Y1) | Out-Null
  Start-Sleep -Milliseconds 70
  [NativeInput]::mouse_event([NativeInput]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 90
  [NativeInput]::SetCursorPos($X2, $Y2) | Out-Null
  Start-Sleep -Milliseconds 120
  [NativeInput]::mouse_event([NativeInput]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
}

function Click-ElementCenter {
  param([System.Windows.Automation.AutomationElement]$Element,[switch]$Double)
  if (-not $Element) { return $false }
  $r = $Element.Current.BoundingRectangle
  if ($r.Width -le 2 -or $r.Height -le 2) { return $false }
  Click-ScreenPoint -X ([int]($r.Left + $r.Width / 2)) -Y ([int]($r.Top + $r.Height / 2)) -Double:$Double
  return $true
}

function Click-InElement {
  param([System.Windows.Automation.AutomationElement]$Element,[double]$XFactor,[double]$YFactor,[switch]$Double)
  if (-not $Element) { return $false }
  $r = $Element.Current.BoundingRectangle
  if ($r.Width -le 2 -or $r.Height -le 2) { return $false }
  Click-ScreenPoint -X ([int]($r.Left + $r.Width * $XFactor)) -Y ([int]($r.Top + $r.Height * $YFactor)) -Double:$Double
  return $true
}

function Get-ElementByName {
  param(
    [System.Windows.Automation.AutomationElement]$Root,
    [string]$Name,
    [System.Windows.Automation.ControlType]$ControlType
  )
  $cond = New-Object System.Windows.Automation.AndCondition(
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $ControlType)),
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name))
  )
  return $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Get-ElementByAutomationId {
  param(
    [System.Windows.Automation.AutomationElement]$Root,
    [string]$AutomationId,
    [System.Windows.Automation.ControlType]$ControlType = $null
  )
  $idCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
  $cond = $idCondition
  if ($ControlType -ne $null) {
    $cond = New-Object System.Windows.Automation.AndCondition(
      (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $ControlType)),
      $idCondition
    )
  }
  return $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Invoke-Element {
  param([System.Windows.Automation.AutomationElement]$Element)
  if (-not $Element) { return $false }
  try {
    $inv = $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    if ($inv) {
      $inv.Invoke()
      return $true
    }
  } catch {}
  try {
    $sel = $Element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    if ($sel) {
      $sel.Select()
      return $true
    }
  } catch {}
  return (Click-ElementCenter -Element $Element)
}

function Wait-MainWindow {
  param([int]$ProcessId,[int]$TimeoutMs = 20000)
  $elapsed = 0
  while ($elapsed -lt $TimeoutMs) {
    Start-Sleep -Milliseconds 250
    $elapsed += 250
    $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
      [System.Windows.Automation.TreeScope]::Children,
      (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId))
    )
    if ($window) { return $window }
  }
  return $null
}

function Open-Overflow {
  param([System.Windows.Automation.AutomationElement]$Window)
  $overflow = Get-ElementByAutomationId -Root $Window -AutomationId 'OverflowButton' -ControlType ([System.Windows.Automation.ControlType]::Button)
  if (-not $overflow) { return $false }
  $ok = Invoke-Element $overflow
  Start-Sleep -Milliseconds 220
  return $ok
}

function Find-ToolbarButton {
  param(
    [System.Windows.Automation.AutomationElement]$Window,
    [string]$Name,
    [string]$AutomationId = '',
    [int]$Attempts = 6
  )

  for ($i = 0; $i -lt $Attempts; $i++) {
    $button = $null
    if (-not [string]::IsNullOrWhiteSpace($AutomationId)) {
      $button = Get-ElementByAutomationId -Root $Window -AutomationId $AutomationId -ControlType ([System.Windows.Automation.ControlType]::Button)
    }
    if (-not $button -and -not [string]::IsNullOrWhiteSpace($Name)) {
      $button = Get-ElementByName -Root $Window -Name $Name -ControlType ([System.Windows.Automation.ControlType]::Button)
    }
    if ($button) { return $button }

    [void](Open-Overflow -Window $Window)
    Start-Sleep -Milliseconds 170
  }

  return $null
}

function Dismiss-ProcessDialogs {
  param(
    [int]$ProcessId,
    [string]$MainWindowName
  )
  $children = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
    [System.Windows.Automation.TreeScope]::Children,
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId))
  )

  $closed = 0
  for ($i = 0; $i -lt $children.Count; $i++) {
    $win = $children.Item($i)
    if ($win.Current.ControlType -ne [System.Windows.Automation.ControlType]::Window) { continue }
    if ($win.Current.Name -eq $MainWindowName) { continue }

    foreach ($caption in @('Yes','OK','Cancel','Close','No')) {
      $btn = Get-ElementByName -Root $win -Name $caption -ControlType ([System.Windows.Automation.ControlType]::Button)
      if ($btn -and (Invoke-Element $btn)) {
        $closed++
        Start-Sleep -Milliseconds 180
        break
      }
    }
  }

  return $closed
}

function Drain-Dialogs {
  param(
    [int]$ProcessId,
    [string]$MainWindowName,
    [int]$Attempts = 8,
    [switch]$SendEscEachRound
  )
  $total = 0
  for ($i = 0; $i -lt $Attempts; $i++) {
    if ($SendEscEachRound) {
      [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
      Start-Sleep -Milliseconds 120
    }
    $closed = Dismiss-ProcessDialogs -ProcessId $ProcessId -MainWindowName $MainWindowName
    $total += $closed
    if ($closed -eq 0 -and -not $SendEscEachRound) { break }
    Start-Sleep -Milliseconds 150
  }
  return $total
}

$repoRoot = Get-Location
$exePath = Join-Path $repoRoot 'ElectricalComponentSandbox\bin\Debug\net8.0-windows\ElectricalComponentSandbox.exe'
if (-not (Test-Path $exePath)) {
  dotnet build ElectricalComponentSandbox/ElectricalComponentSandbox.csproj -c Debug | Out-Host
}

$logDir = Join-Path $env:LOCALAPPDATA 'ElectricalComponentSandbox\Logs'
$existingLogs = @{}
if (Test-Path $logDir) {
  Get-ChildItem $logDir -File | ForEach-Object { $existingLogs[$_.FullName] = $true }
}

$results = New-Object System.Collections.Generic.List[object]
function Add-Result {
  param([string]$Scenario,[string]$Step,[bool]$Ok,[string]$Info = '')
  $results.Add([PSCustomObject]@{
    Scenario = $Scenario
    Step = $Step
    Ok = $Ok
    Info = $Info
  }) | Out-Null
}

function Start-AppContext {
  param([string]$ScenarioName)
  $proc = Start-Process -FilePath $exePath -PassThru
  $window = Wait-MainWindow -ProcessId $proc.Id
  if (-not $window) { throw "[$ScenarioName] Main window not found." }
  [NativeInput]::SetForegroundWindow([IntPtr]::new($window.Current.NativeWindowHandle)) | Out-Null
  return [PSCustomObject]@{
    Proc = $proc
    Window = $window
    MainWindowName = $window.Current.Name
  }
}

function Stop-AppContext {
  param($Context)
  if ($Context -and $Context.Proc -and -not $Context.Proc.HasExited) {
    $Context.Proc.CloseMainWindow() | Out-Null
    Start-Sleep -Milliseconds 900
    if (-not $Context.Proc.HasExited) {
      $Context.Proc.Kill()
    }
  }
}

function Enter-2DTab {
  param($Ctx)
  $tab2d = Get-ElementByName -Root $Ctx.Window -Name '2D Plan View' -ControlType ([System.Windows.Automation.ControlType]::TabItem)
  $ok = Invoke-Element $tab2d
  Start-Sleep -Milliseconds 250
  return $ok
}

function Enter-3DTab {
  param($Ctx)
  $tab3d = Get-ElementByName -Root $Ctx.Window -Name '3D Viewport' -ControlType ([System.Windows.Automation.ControlType]::TabItem)
  $ok = Invoke-Element $tab3d
  Start-Sleep -Milliseconds 220
  return $ok
}

function Get-PlanScrollViewer {
  param($Ctx)
  return Get-ElementByAutomationId -Root $Ctx.Window -AutomationId 'PlanScrollViewer'
}

function Run-Scenario {
  param(
    [string]$Name,
    [scriptblock]$Action
  )
  $ctx = $null
  try {
    $ctx = Start-AppContext -ScenarioName $Name
    Add-Result -Scenario $Name -Step 'Launch main window' -Ok $true -Info $ctx.MainWindowName
    & $Action $ctx
    Add-Result -Scenario $Name -Step 'Process alive after scenario' -Ok (-not $ctx.Proc.HasExited)
  }
  catch {
    Add-Result -Scenario $Name -Step 'Scenario exception' -Ok $false -Info $_.Exception.Message
  }
  finally {
    if ($ctx) {
      [void](Drain-Dialogs -ProcessId $ctx.Proc.Id -MainWindowName $ctx.MainWindowName -Attempts 3 -SendEscEachRound)
    }
    Stop-AppContext -Context $ctx
  }
}

Run-Scenario -Name 'Core' -Action {
  param($ctx)

  foreach ($menu in @('File','Edit','View')) {
    $menuItem = Get-ElementByName -Root $ctx.Window -Name $menu -ControlType ([System.Windows.Automation.ControlType]::MenuItem)
    Add-Result -Scenario 'Core' -Step "Open menu $menu" -Ok (Invoke-Element $menuItem)
    Start-Sleep -Milliseconds 110
  }

  Add-Result -Scenario 'Core' -Step 'Switch to 2D tab' -Ok (Enter-2DTab -Ctx $ctx)
  $plan = Get-PlanScrollViewer -Ctx $ctx
  Add-Result -Scenario 'Core' -Step 'Locate 2D canvas host' -Ok ([bool]$plan)

  $componentButtons = @('Conduit','Box','Panel','Support','Cable Tray','Hanger')
  $x = 0.28
  foreach ($name in $componentButtons) {
    $btn = Find-ToolbarButton -Window $ctx.Window -Name $name
    $inv = Invoke-Element $btn
    Start-Sleep -Milliseconds 100
    $placed = $false
    if ($inv -and $plan) {
      $placed = Click-InElement -Element $plan -XFactor $x -YFactor 0.36
      $x += 0.08
      if ($x -gt 0.74) { $x = 0.28 }
    }
    Add-Result -Scenario 'Core' -Step "Place component $name" -Ok ($inv -and $placed)
  }

  $drawBtn = Find-ToolbarButton -Window $ctx.Window -Name 'Draw Conduit' -AutomationId 'DrawConduitButton'
  $drawMode = Invoke-Element $drawBtn
  if ($drawMode -and $plan) {
    [void](Click-InElement -Element $plan -XFactor 0.34 -YFactor 0.50)
    Start-Sleep -Milliseconds 90
    [void](Click-InElement -Element $plan -XFactor 0.44 -YFactor 0.56)
    Start-Sleep -Milliseconds 90
    [void](Click-InElement -Element $plan -XFactor 0.54 -YFactor 0.60 -Double)
  }
  Add-Result -Scenario 'Core' -Step 'Draw conduit polyline' -Ok $drawMode

  Add-Result -Scenario 'Core' -Step 'Switch to 3D tab' -Ok (Enter-3DTab -Ctx $ctx)
  $viewport = Get-ElementByAutomationId -Root $ctx.Window -AutomationId 'Viewport'
  Add-Result -Scenario 'Core' -Step 'Click 3D viewport' -Ok (Click-InElement -Element $viewport -XFactor 0.5 -YFactor 0.5)

  foreach ($n in @('Undo','Redo')) {
    $btn = Find-ToolbarButton -Window $ctx.Window -Name $n
    Add-Result -Scenario 'Core' -Step "$n action" -Ok (Invoke-Element $btn)
    Start-Sleep -Milliseconds 80
  }
}

Run-Scenario -Name 'Sketch' -Action {
  param($ctx)

  Add-Result -Scenario 'Sketch' -Step 'Switch to 2D tab' -Ok (Enter-2DTab -Ctx $ctx)
  $plan = Get-PlanScrollViewer -Ctx $ctx
  Add-Result -Scenario 'Sketch' -Step 'Locate 2D canvas host' -Ok ([bool]$plan)

  $sketchLine = Find-ToolbarButton -Window $ctx.Window -Name 'Sketch Line' -AutomationId 'SketchLineButton'
  $sl = Invoke-Element $sketchLine
  if ($sl -and $plan) {
    [void](Click-InElement -Element $plan -XFactor 0.30 -YFactor 0.66)
    Start-Sleep -Milliseconds 90
    [void](Click-InElement -Element $plan -XFactor 0.50 -YFactor 0.66)
    Start-Sleep -Milliseconds 90
    [void](Click-InElement -Element $plan -XFactor 0.62 -YFactor 0.66 -Double)
  }
  Add-Result -Scenario 'Sketch' -Step 'Sketch line workflow' -Ok $sl

  $sketchRect = Find-ToolbarButton -Window $ctx.Window -Name 'Sketch Rectangle' -AutomationId 'SketchRectangleButton'
  $sr = Invoke-Element $sketchRect
  if ($sr -and $plan) {
    $r = $plan.Current.BoundingRectangle
    if ($r.Width -gt 2 -and $r.Height -gt 2) {
      Drag-ScreenPoint `
        -X1 ([int]($r.Left + $r.Width * 0.34)) `
        -Y1 ([int]($r.Top + $r.Height * 0.72)) `
        -X2 ([int]($r.Left + $r.Width * 0.50)) `
        -Y2 ([int]($r.Top + $r.Height * 0.82))
    }
  }
  Add-Result -Scenario 'Sketch' -Step 'Sketch rectangle workflow' -Ok $sr

  $convert = Find-ToolbarButton -Window $ctx.Window -Name 'Convert Sketch' -AutomationId 'ConvertSketchButton'
  $cv = Invoke-Element $convert
  $closed = Drain-Dialogs -ProcessId $ctx.Proc.Id -MainWindowName $ctx.MainWindowName -Attempts 6
  Add-Result -Scenario 'Sketch' -Step 'Convert sketch action' -Ok $cv -Info "Dialogs closed: $closed"
}

Run-Scenario -Name 'RoutingExport' -Action {
  param($ctx)

  Add-Result -Scenario 'RoutingExport' -Step 'Switch to 2D tab' -Ok (Enter-2DTab -Ctx $ctx)
  $plan = Get-PlanScrollViewer -Ctx $ctx
  Add-Result -Scenario 'RoutingExport' -Step 'Locate 2D canvas host' -Ok ([bool]$plan)

  $csvButton = Find-ToolbarButton -Window $ctx.Window -Name 'Export Runs CSV' -AutomationId 'ExportRunsCsvButton'
  $jsonButton = Find-ToolbarButton -Window $ctx.Window -Name 'Export Conduit JSON' -AutomationId 'ExportConduitJsonButton'
  Add-Result -Scenario 'RoutingExport' -Step 'CSV export disabled before runs' -Ok ([bool]$csvButton -and -not $csvButton.Current.IsEnabled)
  Add-Result -Scenario 'RoutingExport' -Step 'JSON export disabled before runs' -Ok ([bool]$jsonButton -and -not $jsonButton.Current.IsEnabled)

  $freehand = Find-ToolbarButton -Window $ctx.Window -Name 'Freehand Conduit' -AutomationId 'FreehandConduitButton'
  $fh = Invoke-Element $freehand
  if ($fh -and $plan) {
    [void](Click-InElement -Element $plan -XFactor 0.28 -YFactor 0.42)
    Start-Sleep -Milliseconds 90
    [void](Click-InElement -Element $plan -XFactor 0.36 -YFactor 0.48)
    Start-Sleep -Milliseconds 90
    [void](Click-InElement -Element $plan -XFactor 0.44 -YFactor 0.54 -Double)
    [void](Drain-Dialogs -ProcessId $ctx.Proc.Id -MainWindowName $ctx.MainWindowName -Attempts 3)
  }
  Add-Result -Scenario 'RoutingExport' -Step 'Freehand conduit workflow' -Ok $fh

  $csvButton = Find-ToolbarButton -Window $ctx.Window -Name 'Export Runs CSV' -AutomationId 'ExportRunsCsvButton'
  $jsonButton = Find-ToolbarButton -Window $ctx.Window -Name 'Export Conduit JSON' -AutomationId 'ExportConduitJsonButton'
  Add-Result -Scenario 'RoutingExport' -Step 'CSV export enabled after run' -Ok ([bool]$csvButton -and $csvButton.Current.IsEnabled)
  Add-Result -Scenario 'RoutingExport' -Step 'JSON export enabled after run' -Ok ([bool]$jsonButton -and $jsonButton.Current.IsEnabled)

  $csvOk = Invoke-Element $csvButton
  Start-Sleep -Milliseconds 350
  [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
  $csvClosed = Drain-Dialogs -ProcessId $ctx.Proc.Id -MainWindowName $ctx.MainWindowName -Attempts 5 -SendEscEachRound
  Add-Result -Scenario 'RoutingExport' -Step 'Export Runs CSV dialog' -Ok $csvOk -Info "Dialogs closed: $csvClosed; sent ESC=True"

  $jsonOk = Invoke-Element $jsonButton
  Start-Sleep -Milliseconds 350
  [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
  $jsonClosed = Drain-Dialogs -ProcessId $ctx.Proc.Id -MainWindowName $ctx.MainWindowName -Attempts 5 -SendEscEachRound
  Add-Result -Scenario 'RoutingExport' -Step 'Export Conduit JSON dialog' -Ok $jsonOk -Info "Dialogs closed: $jsonClosed; sent ESC=True"

  $autoRoute = Find-ToolbarButton -Window $ctx.Window -Name 'Auto-Route' -AutomationId 'AutoRouteButton'
  $ar = Invoke-Element $autoRoute
  $arClosed = Drain-Dialogs -ProcessId $ctx.Proc.Id -MainWindowName $ctx.MainWindowName -Attempts 8
  Add-Result -Scenario 'RoutingExport' -Step 'Auto-route workflow' -Ok $ar -Info "Dialogs closed: $arClosed"
}

$newLogs = @()
if (Test-Path $logDir) {
  $newLogs = Get-ChildItem $logDir -File |
    Sort-Object LastWriteTime -Descending |
    Where-Object { -not $existingLogs.ContainsKey($_.FullName) }
}

$errorLines = @()
foreach ($log in $newLogs) {
  $lines = Get-Content $log.FullName | Where-Object { $_ -match '\[ERROR\s*\]' -or $_ -match 'Unhandled' }
  $errorLines += $lines
}

Write-Host "`n=== Expanded UI Smoke Results (Deterministic) ==="
$results | Format-Table -AutoSize | Out-String -Width 280 | Write-Host
Write-Host "New log files: $($newLogs.Count)"
if ($newLogs.Count -gt 0) {
  ($newLogs | Select-Object -First 5 | ForEach-Object { $_.FullName }) | ForEach-Object { Write-Host "  $_" }
}
Write-Host "Error-like log entries: $($errorLines.Count)"
if ($errorLines.Count -gt 0) {
  $errorLines | Select-Object -First 30 | ForEach-Object { Write-Host $_ }
}

$failed = $results | Where-Object { -not $_.Ok }
if ($failed.Count -gt 0 -or $errorLines.Count -gt 0) { exit 2 }
exit 0
