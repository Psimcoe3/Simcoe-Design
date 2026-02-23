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

function Report-Target {
  param([System.Windows.Automation.AutomationElement]$Window,[string]$Name)
  [void](Open-Overflow -Window $Window)
  $btn = Get-Element -Root $Window -Name $Name -ControlType ([System.Windows.Automation.ControlType]::Button)
  if (-not $btn) {
    Write-Host "$Name | found=False"
    return
  }
  $r = $btn.Current.BoundingRectangle
  $invoke = Invoke-Element $btn
  Write-Host ("{0} | found=True enabled={1} offscreen={2} rect={3}x{4} invoke={5}" -f $Name, $btn.Current.IsEnabled, $btn.Current.IsOffscreen, [int]$r.Width, [int]$r.Height, $invoke)
  Start-Sleep -Milliseconds 250
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
  Start-Sleep -Milliseconds 300

  foreach ($name in @('Freehand Conduit','Auto-Route','Export Runs CSV','Export Conduit JSON')) {
    Report-Target -Window $window -Name $name
  }

  $root = [System.Windows.Automation.AutomationElement]::RootElement
  $children = $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
  Write-Host 'Top-level windows:'
  for ($i = 0; $i -lt $children.Count; $i++) {
    $c = $children.Item($i)
    if ($c.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window) {
      Write-Host ("  PID={0} Name={1}" -f $c.Current.ProcessId, $c.Current.Name)
    }
  }
}
finally {
  if ($proc -and -not $proc.HasExited) {
    $proc.CloseMainWindow() | Out-Null
    Start-Sleep -Milliseconds 900
    if (-not $proc.HasExited) { $proc.Kill() }
  }
}
