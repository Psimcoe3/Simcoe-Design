param()

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$signature = @"
using System;
using System.Runtime.InteropServices;
public static class NativeInput {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
  public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
  public const uint MOUSEEVENTF_LEFTUP = 0x0004;
}
"@
Add-Type -TypeDefinition $signature -ErrorAction SilentlyContinue | Out-Null

function Click-ScreenPoint {
  param([int]$X,[int]$Y)
  [NativeInput]::SetCursorPos($X, $Y) | Out-Null
  Start-Sleep -Milliseconds 70
  [NativeInput]::mouse_event([NativeInput]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
  [NativeInput]::mouse_event([NativeInput]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
}

function Click-ElementCenter {
  param([System.Windows.Automation.AutomationElement]$Element)
  if (-not $Element) { return $false }
  $r = $Element.Current.BoundingRectangle
  if ($r.Width -le 2 -or $r.Height -le 2) { return $false }
  Click-ScreenPoint -X ([int]($r.Left + $r.Width / 2)) -Y ([int]($r.Top + $r.Height / 2))
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
  return (Click-ElementCenter -Element $Element)
}

function Click-InElement {
  param([System.Windows.Automation.AutomationElement]$Element,[double]$XFactor,[double]$YFactor)
  if (-not $Element) { return $false }
  $r = $Element.Current.BoundingRectangle
  if ($r.Width -le 2 -or $r.Height -le 2) { return $false }
  Click-ScreenPoint -X ([int]($r.Left + $r.Width * $XFactor)) -Y ([int]($r.Top + $r.Height * $YFactor))
  return $true
}

function Open-Overflow {
  param([System.Windows.Automation.AutomationElement]$Window)
  $overflow = $Window.FindFirst(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.AndCondition(
      (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)),
      (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'OverflowButton'))
    ))
  )
  if (-not $overflow) { return $false }
  $ok = Invoke-Element $overflow
  Start-Sleep -Milliseconds 220
  return $ok
}

function Check-RunsCsv {
  param([System.Windows.Automation.AutomationElement]$Window,[string]$Label)
  [void](Open-Overflow -Window $Window)
  $btn = Get-Element -Root $Window -Name 'Export Runs CSV' -ControlType ([System.Windows.Automation.ControlType]::Button)
  if ($btn) {
    Write-Host "$Label | found=True enabled=$($btn.Current.IsEnabled) offscreen=$($btn.Current.IsOffscreen)"
  } else {
    Write-Host "$Label | found=False"
  }
}

function Drag-InElement {
  param(
    [System.Windows.Automation.AutomationElement]$Element,
    [double]$X1Factor,[double]$Y1Factor,
    [double]$X2Factor,[double]$Y2Factor
  )
  if (-not $Element) { return $false }
  $r = $Element.Current.BoundingRectangle
  if ($r.Width -le 2 -or $r.Height -le 2) { return $false }
  Click-ScreenPoint -X ([int]($r.Left + $r.Width * $X1Factor)) -Y ([int]($r.Top + $r.Height * $Y1Factor))
  Start-Sleep -Milliseconds 80
  [NativeInput]::mouse_event([NativeInput]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 90
  [NativeInput]::SetCursorPos([int]($r.Left + $r.Width * $X2Factor), [int]($r.Top + $r.Height * $Y2Factor)) | Out-Null
  Start-Sleep -Milliseconds 130
  [NativeInput]::mouse_event([NativeInput]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
  return $true
}

$exe = Join-Path (Get-Location) 'ElectricalComponentSandbox\bin\Debug\net8.0-windows\ElectricalComponentSandbox.exe'
if (-not (Test-Path $exe)) {
  dotnet build ElectricalComponentSandbox/ElectricalComponentSandbox.csproj -c Debug | Out-Host
}

$proc = Start-Process -FilePath $exe -PassThru
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

  $tab2d = Get-Element -Root $window -Name '2D Plan View' -ControlType ([System.Windows.Automation.ControlType]::TabItem)
  [void](Invoke-Element $tab2d)
  Start-Sleep -Milliseconds 250

  $planScroll = $window.FindFirst(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'PlanScrollViewer'))
  )

  Check-RunsCsv -Window $window -Label 'Initial'

  foreach ($b in @('Conduit','Box','Panel')) {
    $btn = Get-Element -Root $window -Name $b -ControlType ([System.Windows.Automation.ControlType]::Button)
    [void](Invoke-Element $btn)
    Start-Sleep -Milliseconds 90
    [void](Click-InElement -Element $planScroll -XFactor 0.35 -YFactor 0.35)
  }

  $draw = Get-Element -Root $window -Name 'Draw Conduit' -ControlType ([System.Windows.Automation.ControlType]::Button)
  [void](Invoke-Element $draw)
  [void](Click-InElement -Element $planScroll -XFactor 0.32 -YFactor 0.50)
  Start-Sleep -Milliseconds 90
  [void](Click-InElement -Element $planScroll -XFactor 0.52 -YFactor 0.55)

  Check-RunsCsv -Window $window -Label 'After draw'

  [void](Open-Overflow -Window $window)
  $sketchLine = Get-Element -Root $window -Name 'Sketch Line' -ControlType ([System.Windows.Automation.ControlType]::Button)
  [void](Invoke-Element $sketchLine)
  [void](Click-InElement -Element $planScroll -XFactor 0.35 -YFactor 0.62)
  Start-Sleep -Milliseconds 90
  [void](Click-InElement -Element $planScroll -XFactor 0.52 -YFactor 0.62)
  Check-RunsCsv -Window $window -Label 'After sketch line'

  [void](Open-Overflow -Window $window)
  $sketchRect = Get-Element -Root $window -Name 'Sketch Rectangle' -ControlType ([System.Windows.Automation.ControlType]::Button)
  [void](Invoke-Element $sketchRect)
  [void](Drag-InElement -Element $planScroll -X1Factor 0.35 -Y1Factor 0.68 -X2Factor 0.48 -Y2Factor 0.78)
  Check-RunsCsv -Window $window -Label 'After sketch rectangle'

  [void](Open-Overflow -Window $window)
  $convert = Get-Element -Root $window -Name 'Convert Sketch' -ControlType ([System.Windows.Automation.ControlType]::Button)
  [void](Invoke-Element $convert)
  Start-Sleep -Milliseconds 300
  Check-RunsCsv -Window $window -Label 'After convert sketch'

  [void](Open-Overflow -Window $window)
  $auto = Get-Element -Root $window -Name 'Auto-Route' -ControlType ([System.Windows.Automation.ControlType]::Button)
  [void](Invoke-Element $auto)
  Start-Sleep -Milliseconds 350

  Check-RunsCsv -Window $window -Label 'After auto-route'
}
finally {
  if ($proc -and -not $proc.HasExited) {
    $proc.CloseMainWindow() | Out-Null
    Start-Sleep -Milliseconds 900
    if (-not $proc.HasExited) { $proc.Kill() }
  }
}
