Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$sig=@"
using System;
using System.Runtime.InteropServices;
public static class NativeInput {
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
 [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
 public const uint LEFTDOWN=0x0002; public const uint LEFTUP=0x0004;
}
"@
Add-Type -TypeDefinition $sig -ErrorAction SilentlyContinue | Out-Null

function Click($x,$y){ [NativeInput]::SetCursorPos($x,$y)|Out-Null; Start-Sleep -Milliseconds 80; [NativeInput]::mouse_event([NativeInput]::LEFTDOWN,0,0,0,[UIntPtr]::Zero); [NativeInput]::mouse_event([NativeInput]::LEFTUP,0,0,0,[UIntPtr]::Zero) }

$exe = Join-Path (Get-Location) 'ElectricalComponentSandbox\\bin\\Debug\\net8.0-windows\\ElectricalComponentSandbox.exe'
$proc=Start-Process $exe -PassThru
try {
  $window=$null
  for($i=0;$i -lt 60;$i++){ Start-Sleep -Milliseconds 300; $window=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$proc.Id))); if($window){break}}
  if(-not $window){throw 'no window'}

  $overflow=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.AndCondition((New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button)),(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'OverflowButton')))))
  if($overflow){
    $r=$overflow.Current.BoundingRectangle
    Click ([int]($r.Left+$r.Width/2)) ([int]($r.Top+$r.Height/2))
    Start-Sleep -Milliseconds 500
  }

  $all=$window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
  for($i=0;$i -lt $all.Count;$i++){
    $el=$all.Item($i)
    if($el.Current.ControlType -eq [System.Windows.Automation.ControlType]::MenuItem -or $el.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button){
      if(-not [string]::IsNullOrWhiteSpace($el.Current.Name)){
        '{0} | {1} | {2}' -f $el.Current.ControlType.ProgrammaticName,$el.Current.Name,$el.Current.AutomationId
      }
    }
  }
}
finally {
  if($proc -and -not $proc.HasExited){ $proc.CloseMainWindow()|Out-Null; Start-Sleep -Milliseconds 800; if(-not $proc.HasExited){$proc.Kill()} }
}
