param()

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

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
  Start-Sleep -Milliseconds 80
  [NativeInput]::mouse_event([NativeInput]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 100
  [NativeInput]::SetCursorPos($X2, $Y2) | Out-Null
  Start-Sleep -Milliseconds 140
  [NativeInput]::mouse_event([NativeInput]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
}

function Click-ElementCenter {
  param([System.Windows.Automation.AutomationElement]$Element,[switch]$Double)
  if (-not $Element) { return $false }
  $r = $Element.Current.BoundingRectangle
  if ($r.Width -le 2 -or $r.Height -le 2) { return $false }
  Click-ScreenPoint -X ([int]($r.Left + $r.Width/2)) -Y ([int]($r.Top + $r.Height/2)) -Double:$Double
  return $true
}

function Get-Element {
  param([System.Windows.Automation.AutomationElement]$Root,[string]$Name,[System.Windows.Automation.ControlType]$ControlType)
  $cond = New-Object System.Windows.Automation.AndCondition(
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $ControlType)),
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name))
  )
  return $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Invoke-Element {
  param([System.Windows.Automation.AutomationElement]$Element)
  if (-not $Element) { return $false }
  try {
    $inv = $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    if ($inv) { $inv.Invoke(); return $true }
  } catch {}
  try {
    $sel = $Element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    if ($sel) { $sel.Select(); return $true }
  } catch {}
  return (Click-ElementCenter -Element $Element)
}

function Click-InElement {
  param([System.Windows.Automation.AutomationElement]$Element,[double]$XFactor,[double]$YFactor,[switch]$Double)
  if (-not $Element) { return $false }
  $rect = $Element.Current.BoundingRectangle
  if ($rect.Width -le 2 -or $rect.Height -le 2) { return $false }
  $x = [int]($rect.Left + ($rect.Width * $XFactor))
  $y = [int]($rect.Top + ($rect.Height * $YFactor))
  Click-ScreenPoint -X $x -Y $y -Double:$Double
  return $true
}

function Drag-InElement {
  param([System.Windows.Automation.AutomationElement]$Element,[double]$X1,[double]$Y1,[double]$X2,[double]$Y2)
  if (-not $Element) { return $false }
  $r = $Element.Current.BoundingRectangle
  if ($r.Width -le 2 -or $r.Height -le 2) { return $false }
  Drag-ScreenPoint \
    -X1 ([int]($r.Left + $r.Width * $X1)) \
    -Y1 ([int]($r.Top + $r.Height * $Y1)) \
    -X2 ([int]($r.Left + $r.Width * $X2)) \
    -Y2 ([int]($r.Top + $r.Height * $Y2))
  return $true
}

function Dismiss-ProcessDialogs {
  param([int]$ProcessId,[string]$MainWindowName)

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
      $btn = $win.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.AndCondition(
          (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)),
          (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $caption))
        ))
      )
      if ($btn -and (Invoke-Element $btn)) {
        $closed++
        Start-Sleep -Milliseconds 200
        break
      }
    }
  }

  return $closed
}

function Drain-Dialogs {
  param([int]$ProcessId,[string]$MainWindowName,[int]$Attempts=5)
  $total = 0
  for ($i = 0; $i -lt $Attempts; $i++) {
    Start-Sleep -Milliseconds 200
    $closed = Dismiss-ProcessDialogs -ProcessId $ProcessId -MainWindowName $MainWindowName
    $total += $closed
    if ($closed -eq 0) { break }
  }
  return $total
}

$repoRoot = Get-Location
$exe = Join-Path $repoRoot 'ElectricalComponentSandbox\\bin\\Debug\\net8.0-windows\\ElectricalComponentSandbox.exe'
if (-not (Test-Path $exe)) {
  dotnet build ElectricalComponentSandbox/ElectricalComponentSandbox.csproj -c Debug | Out-Host
}

$logDir = Join-Path $env:LOCALAPPDATA 'ElectricalComponentSandbox\\Logs'
$existingLogs = @{}
if (Test-Path $logDir) {
  Get-ChildItem -Path $logDir -File | ForEach-Object { $existingLogs[$_.FullName] = $true }
}

$results = New-Object System.Collections.Generic.List[object]
function Add-Result([string]$Step,[bool]$Ok,[string]$Info='') { $results.Add([PSCustomObject]@{ Step=$Step; Ok=$Ok; Info=$Info }) | Out-Null }

$proc = Start-Process -FilePath $exe -PassThru
$mainWindowName = 'Electrical Component Sandbox'

try {
  $window = $null
  for ($i = 0; $i -lt 80; $i++) {
    Start-Sleep -Milliseconds 250
    $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
      [System.Windows.Automation.TreeScope]::Children,
      (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id))
    )
    if ($window) { break }
  }
  if (-not $window) { throw 'Main window not found.' }

  [NativeInput]::SetForegroundWindow([IntPtr]::new($window.Current.NativeWindowHandle)) | Out-Null
  Add-Result 'Launch main window' $true $window.Current.Name

  foreach ($menu in @('File','Edit','View')) {
    $el = Get-Element -Root $window -Name $menu -ControlType ([System.Windows.Automation.ControlType]::MenuItem)
    $ok = Invoke-Element $el
    Add-Result "Open menu $menu" $ok
    Start-Sleep -Milliseconds 160
  }

  $tab2d = Get-Element -Root $window -Name '2D Plan View' -ControlType ([System.Windows.Automation.ControlType]::TabItem)
  Add-Result 'Switch to 2D tab' (Invoke-Element $tab2d)
  Start-Sleep -Milliseconds 320

  $planScroll = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'PlanScrollViewer')))
  Add-Result 'Locate 2D canvas host' ([bool]$planScroll)

  # Place component from each add button
  $addButtons = @('Conduit','Box','Panel','Support','Cable Tray','Hanger')
  $x = 0.30
  foreach ($b in $addButtons) {
    $btn = Get-Element -Root $window -Name $b -ControlType ([System.Windows.Automation.ControlType]::Button)
    $inv = Invoke-Element $btn
    Start-Sleep -Milliseconds 120
    $placed = $false
    if ($inv -and $planScroll) {
      $placed = Click-InElement -Element $planScroll -XFactor $x -YFactor 0.35
      $x += 0.08; if ($x -gt 0.75) { $x = 0.30 }
    }
    Add-Result "Place component $b" ($inv -and $placed)
    Start-Sleep -Milliseconds 160
  }

  # Draw conduit workflow
  $drawBtn = Get-Element -Root $window -Name 'Draw Conduit' -ControlType ([System.Windows.Automation.ControlType]::Button)
  $drawMode = Invoke-Element $drawBtn
  if ($drawMode -and $planScroll) {
    Click-InElement -Element $planScroll -XFactor 0.32 -YFactor 0.48 | Out-Null
    Start-Sleep -Milliseconds 100
    Click-InElement -Element $planScroll -XFactor 0.42 -YFactor 0.52 | Out-Null
    Start-Sleep -Milliseconds 100
    Click-InElement -Element $planScroll -XFactor 0.52 -YFactor 0.58 -Double | Out-Null
  }
  Add-Result 'Draw conduit polyline' $drawMode

  # Sketch line + convert
  $sketchLine = Get-Element -Root $window -Name 'Sketch Line' -ControlType ([System.Windows.Automation.ControlType]::Button)
  $sketchLineOn = Invoke-Element $sketchLine
  if ($sketchLineOn -and $planScroll) {
    Click-InElement -Element $planScroll -XFactor 0.35 -YFactor 0.62 | Out-Null
    Start-Sleep -Milliseconds 100
    Click-InElement -Element $planScroll -XFactor 0.52 -YFactor 0.62 | Out-Null
    Start-Sleep -Milliseconds 100
    Click-InElement -Element $planScroll -XFactor 0.60 -YFactor 0.62 -Double | Out-Null
  }
  Add-Result 'Sketch line workflow' $sketchLineOn

  $sketchRect = Get-Element -Root $window -Name 'Sketch Rectangle' -ControlType ([System.Windows.Automation.ControlType]::Button)
  $sketchRectOn = Invoke-Element $sketchRect
  if ($sketchRectOn -and $planScroll) {
    Drag-InElement -Element $planScroll -X1 0.35 -Y1 0.68 -X2 0.48 -Y2 0.78 | Out-Null
  }
  Add-Result 'Sketch rectangle workflow' $sketchRectOn

  $convertSketch = Get-Element -Root $window -Name 'Convert Sketch' -ControlType ([System.Windows.Automation.ControlType]::Button)
  $conv = Invoke-Element $convertSketch
  $dlgClosed = Drain-Dialogs -ProcessId $proc.Id -MainWindowName $window.Current.Name -Attempts 8
  Add-Result 'Convert sketch action' $conv "Dialogs closed: $dlgClosed"

  # Freehand conduit
  $freehand = Get-Element -Root $window -Name 'Freehand Conduit' -ControlType ([System.Windows.Automation.ControlType]::Button)
  $fh = Invoke-Element $freehand
  if ($fh -and $planScroll) {
    Click-InElement -Element $planScroll -XFactor 0.30 -YFactor 0.45 | Out-Null
    Start-Sleep -Milliseconds 90
    Click-InElement -Element $planScroll -XFactor 0.36 -YFactor 0.50 | Out-Null
    Start-Sleep -Milliseconds 90
    Click-InElement -Element $planScroll -XFactor 0.42 -YFactor 0.56 -Double | Out-Null
  }
  Add-Result 'Freehand conduit workflow' $fh

  # Auto-route and close info dialog
  $autoroute = Get-Element -Root $window -Name 'Auto-Route' -ControlType ([System.Windows.Automation.ControlType]::Button)
  $ar = Invoke-Element $autoroute
  $arClosed = Drain-Dialogs -ProcessId $proc.Id -MainWindowName $window.Current.Name -Attempts 8
  Add-Result 'Auto-route workflow' $ar "Dialogs closed: $arClosed"

  # Export actions: ensure dialogs open and can be cancelled
  foreach ($exportName in @('Export Runs CSV','Export Conduit JSON')) {
    $btn = Get-Element -Root $window -Name $exportName -ControlType ([System.Windows.Automation.ControlType]::Button)
    $ok = Invoke-Element $btn
    $closed = Drain-Dialogs -ProcessId $proc.Id -MainWindowName $window.Current.Name -Attempts 10
    Add-Result "$exportName dialog" $ok "Dialogs closed: $closed"
  }

  $tab3d = Get-Element -Root $window -Name '3D Viewport' -ControlType ([System.Windows.Automation.ControlType]::TabItem)
  Add-Result 'Switch to 3D tab' (Invoke-Element $tab3d)
  Start-Sleep -Milliseconds 280

  $viewport = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'Viewport')))
  Add-Result 'Click 3D viewport' (Click-InElement -Element $viewport -XFactor 0.5 -YFactor 0.5)

  foreach ($name in @('Undo','Redo')) {
    $btn = Get-Element -Root $window -Name $name -ControlType ([System.Windows.Automation.ControlType]::Button)
    Add-Result "$name action" (Invoke-Element $btn)
    Start-Sleep -Milliseconds 100
  }

  Add-Result 'Process alive after UI workflow' (-not $proc.HasExited)
}
catch {
  Add-Result 'Automation exception' $false $_.Exception.Message
}
finally {
  if ($proc -and -not $proc.HasExited) {
    $proc.CloseMainWindow() | Out-Null
    Start-Sleep -Milliseconds 900
    if (-not $proc.HasExited) { $proc.Kill() }
  }
}

$newLog = $null
if (Test-Path $logDir) {
  $newLog = Get-ChildItem -Path $logDir -File |
    Sort-Object LastWriteTime -Descending |
    Where-Object { -not $existingLogs.ContainsKey($_.FullName) } |
    Select-Object -First 1
  if (-not $newLog) {
    $newLog = Get-ChildItem -Path $logDir -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  }
}

$errorLines = @()
if ($newLog -and (Test-Path $newLog.FullName)) {
  $errorLines = Get-Content -Path $newLog.FullName | Where-Object { $_ -match '\[ERROR\s*\]' -or $_ -match 'Unhandled' }
}
$errorCount = $errorLines.Count

Write-Host ''
Write-Host '=== Full UI Smoke Results ==='
$results | Format-Table -AutoSize | Out-String -Width 240 | Write-Host
Write-Host "Log file: $($newLog.FullName)"
Write-Host "Error-like log entries: $errorCount"
if ($errorCount -gt 0) {
  $errorLines | Select-Object -First 30 | ForEach-Object { Write-Host $_ }
}

$failed = $results | Where-Object { -not $_.Ok }
if ($failed.Count -gt 0 -or $errorCount -gt 0) { exit 2 }
exit 0
