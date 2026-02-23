param()

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Find-ElementByName {
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

function Try-Invoke {
  param([System.Windows.Automation.AutomationElement]$Element)
  if (-not $Element) { return $false }
  try {
    $inv = $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    if ($inv) {
      $inv.Invoke()
      return $true
    }
  } catch {}
  return $false
}

$exe = Join-Path (Get-Location) 'ElectricalComponentSandbox\bin\Debug\net8.0-windows\ElectricalComponentSandbox.exe'
if (-not (Test-Path $exe)) {
  dotnet build ElectricalComponentSandbox/ElectricalComponentSandbox.csproj -c Debug | Out-Host
}

$proc = Start-Process -FilePath $exe -PassThru

try {
  $root = [System.Windows.Automation.AutomationElement]::RootElement
  $window = $null
  for ($i = 0; $i -lt 80; $i++) {
    Start-Sleep -Milliseconds 250
    $window = $root.FindFirst(
      [System.Windows.Automation.TreeScope]::Children,
      (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id))
    )
    if ($window) { break }
  }
  if (-not $window) { throw 'Main window not found.' }

  $tab2d = Find-ElementByName -Root $window -Name '2D Plan View' -ControlType ([System.Windows.Automation.ControlType]::TabItem)
  [void](Try-Invoke $tab2d)
  Start-Sleep -Milliseconds 250

  $overflow = $window.FindFirst(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'OverflowButton'))
  )
  [void](Try-Invoke $overflow)
  Start-Sleep -Milliseconds 250

  foreach ($name in @('Freehand Conduit','Auto-Route','Export Runs CSV','Export Conduit JSON')) {
    $btn = Find-ElementByName -Root $window -Name $name -ControlType ([System.Windows.Automation.ControlType]::Button)
    if (-not $btn) {
      Write-Host "$name | NOT FOUND"
      continue
    }
    Write-Host "$name | Enabled=$($btn.Current.IsEnabled) Offscreen=$($btn.Current.IsOffscreen) AutomationId=$($btn.Current.AutomationId)"
    $ok = Try-Invoke $btn
    Write-Host "  Invoke=$ok"
    Start-Sleep -Milliseconds 350

    $children = $root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    $wins = @()
    for ($i = 0; $i -lt $children.Count; $i++) {
      $c = $children.Item($i)
      if ($c.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and $c.Current.ProcessId -ne $proc.Id) {
        $wins += ("PID={0} Name={1}" -f $c.Current.ProcessId, $c.Current.Name)
      }
    }
    if ($wins.Count -gt 0) {
      Write-Host '  External windows:'
      $wins | ForEach-Object { Write-Host "    $_" }
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
