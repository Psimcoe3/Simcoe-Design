Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$exe = Join-Path (Get-Location) 'ElectricalComponentSandbox\bin\Debug\net8.0-windows\ElectricalComponentSandbox.exe'
if (-not (Test-Path $exe)) {
  dotnet build ElectricalComponentSandbox/ElectricalComponentSandbox.csproj -c Debug | Out-Host
}

$proc = Start-Process -FilePath $exe -PassThru
try {
  $window = $null
  for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Milliseconds 500
    $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
      [System.Windows.Automation.TreeScope]::Children,
      (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $proc.Id
      ))
    )
    if ($window) { break }
  }

  if (-not $window) {
    Write-Host 'Main window not found.'
    exit 1
  }

  Write-Host "Window: $($window.Current.Name)"

  $all = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  $interesting = @()
  for ($i = 0; $i -lt $all.Count; $i++) {
    $el = $all.Item($i)
    $ct = $el.Current.ControlType.ProgrammaticName
    if ($ct -in @(
      'ControlType.Button',
      'ControlType.MenuItem',
      'ControlType.TabItem',
      'ControlType.TextBox',
      'ControlType.ComboBox',
      'ControlType.CheckBox',
      'ControlType.Slider',
      'ControlType.ListItem'
    )) {
      $interesting += [PSCustomObject]@{
        ControlType = $ct
        Name = $el.Current.Name
        AutomationId = $el.Current.AutomationId
      }
    }
  }

  $interesting | Sort-Object ControlType, Name, AutomationId | Format-Table -AutoSize | Out-String -Width 220 | Write-Host
}
finally {
  if ($proc -and -not $proc.HasExited) {
    $proc.CloseMainWindow() | Out-Null
    Start-Sleep -Milliseconds 700
    if (-not $proc.HasExited) { $proc.Kill() }
  }
}
