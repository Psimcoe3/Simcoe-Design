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
  Start-Sleep -Milliseconds 80
  [NativeInput]::mouse_event([NativeInput]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
  [NativeInput]::mouse_event([NativeInput]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
  if ($Double) {
    Start-Sleep -Milliseconds 120
    [NativeInput]::mouse_event([NativeInput]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    [NativeInput]::mouse_event([NativeInput]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
  }
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
    foreach ($caption in @('OK','Yes','Close')) {
      $btn = $win.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.AndCondition(
          (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)),
          (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $caption))
        ))
      )
      if ($btn -and (Invoke-Element $btn)) { $closed++; Start-Sleep -Milliseconds 200; break }
    }
  }
  return $closed
}

function Click-InElement {
  param([System.Windows.Automation.AutomationElement]$Element,[double]$XFactor,[double]$YFactor,[switch]$Double)
  if (-not $Element) { return $false }
  $rect = $Element.Current.BoundingRectangle
  if ($rect.Width -le 2 -or $rect.Height -le 2) { return $false }
  Click-ScreenPoint -X ([int]($rect.Left + ($rect.Width * $XFactor))) -Y ([int]($rect.Top + ($rect.Height * $YFactor))) -Double:$Double
  return $true
}

$repoRoot = Get-Location
$exe = Join-Path $repoRoot 'ElectricalComponentSandbox\\bin\\Debug\\net8.0-windows\\ElectricalComponentSandbox.exe'
if (-not (Test-Path $exe)) { dotnet build ElectricalComponentSandbox/ElectricalComponentSandbox.csproj -c Debug | Out-Host }

$logDir = Join-Path $env:LOCALAPPDATA 'ElectricalComponentSandbox\\Logs'
$existingLogs = @{}
if (Test-Path $logDir) { Get-ChildItem -Path $logDir -File | ForEach-Object { $existingLogs[$_.FullName] = $true } }

$results = New-Object System.Collections.Generic.List[object]
function Add-Result([string]$Step,[bool]$Ok,[string]$Info='') { $results.Add([PSCustomObject]@{ Step = $Step; Ok = $Ok; Info = $Info }) | Out-Null }

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
    Start-Sleep -Milliseconds 180
  }

  $tab2d = Get-Element -Root $window -Name '2D Plan View' -ControlType ([System.Windows.Automation.ControlType]::TabItem)
  $ok2d = Invoke-Element $tab2d
  Add-Result 'Switch to 2D tab' $ok2d
  Start-Sleep -Milliseconds 350

  $planScroll = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'PlanScrollViewer')))
  Add-Result 'Locate 2D canvas host' ([bool]$planScroll)

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
    Start-Sleep -Milliseconds 180
  }

  $drawBtn = Get-Element -Root $window -Name 'Draw Conduit' -ControlType ([System.Windows.Automation.ControlType]::Button)
  $drawMode = Invoke-Element $drawBtn
  if ($drawMode -and $planScroll) {
    Click-InElement -Element $planScroll -XFactor 0.32 -YFactor 0.48 | Out-Null
    Start-Sleep -Milliseconds 120
    Click-InElement -Element $planScroll -XFactor 0.42 -YFactor 0.52 | Out-Null
    Start-Sleep -Milliseconds 120
    Click-InElement -Element $planScroll -XFactor 0.52 -YFactor 0.58 -Double | Out-Null
    Start-Sleep -Milliseconds 220
  }
  Add-Result 'Draw conduit polyline' $drawMode

  $tab3d = Get-Element -Root $window -Name '3D Viewport' -ControlType ([System.Windows.Automation.ControlType]::TabItem)
  $ok3d = Invoke-Element $tab3d
  Add-Result 'Switch to 3D tab' $ok3d
  Start-Sleep -Milliseconds 300

  $viewport = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'Viewport')))
  $vpClick = $false
  if ($viewport) { $vpClick = Click-InElement -Element $viewport -XFactor 0.5 -YFactor 0.5 }
  Add-Result 'Click 3D viewport' $vpClick

  foreach ($name in @('Undo','Redo')) {
    $btn = Get-Element -Root $window -Name $name -ControlType ([System.Windows.Automation.ControlType]::Button)
    $ok = Invoke-Element $btn
    Add-Result "$name action" $ok
    Start-Sleep -Milliseconds 120
  }

  $dismissed = Dismiss-ProcessDialogs -ProcessId $proc.Id -MainWindowName $window.Current.Name
  Add-Result 'Dismiss transient dialogs' $true "Closed $dismissed dialog(s)"
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
  if (-not $newLog) { $newLog = Get-ChildItem -Path $logDir -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1 }
}

$errorCount = 0
$errorLines = @()
if ($newLog -and (Test-Path $newLog.FullName)) {
  $errorLines = Get-Content -Path $newLog.FullName | Where-Object { $_ -match '\[ERROR\s*\]' -or $_ -match 'Unhandled' }
  $errorCount = $errorLines.Count
}

Write-Host ''
Write-Host '=== UI Smoke Results ==='
$results | Format-Table -AutoSize | Out-String -Width 220 | Write-Host
Write-Host "Log file: $($newLog.FullName)"
Write-Host "Error-like log entries: $errorCount"
if ($errorCount -gt 0) { $errorLines | Select-Object -First 20 | ForEach-Object { Write-Host $_ } }

$failed = $results | Where-Object { -not $_.Ok }
if ($failed.Count -gt 0 -or $errorCount -gt 0) { exit 2 }
exit 0
