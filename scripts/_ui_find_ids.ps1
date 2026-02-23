Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$exe = Join-Path (Get-Location) 'ElectricalComponentSandbox\bin\Debug\net8.0-windows\ElectricalComponentSandbox.exe'
$proc = Start-Process -FilePath $exe -PassThru
try {
  $window = $null
  for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 300
    $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
      [System.Windows.Automation.TreeScope]::Children,
      (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $proc.Id
      ))
    )
    if ($window) { break }
  }
  if (-not $window) { throw 'Window not found' }

  $all = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
  for ($i=0; $i -lt $all.Count; $i++) {
    $el = $all.Item($i)
    if ($el.Current.AutomationId -in @('PlanCanvas','Viewport','ViewTabs','PlanScrollViewer','LibraryListBox','LayerListBox','PropertiesPanel','MainContentGrid')) {
      $b = $el.Current.BoundingRectangle
      Write-Host "$($el.Current.AutomationId) | $($el.Current.ControlType.ProgrammaticName) | Name='$($el.Current.Name)' | Rect=$($b.Left),$($b.Top),$($b.Right),$($b.Bottom)"
    }
  }
}
finally {
  if ($proc -and -not $proc.HasExited) {
    $proc.CloseMainWindow() | Out-Null
    Start-Sleep -Milliseconds 700
    if (-not $proc.HasExited) { $proc.Kill() }
  }
}
