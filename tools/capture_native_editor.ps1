# Capture the actual foreground WinUI window, never a mockup.
[CmdletBinding(DefaultParameterSetName='Launch')]
param(
    [Parameter(Mandatory=$true, ParameterSetName='Launch')][string]$Executable,
    [Parameter(Mandatory=$true, ParameterSetName='Attach')][int]$ProcessId,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_-]{0,63}$')][string]$Name = '00-startup',
    [ValidateRange(5,120)][int]$TimeoutSeconds = 45,
    [string]$Commit = '',
    [switch]$CaptureModules
)
$ErrorActionPreference = 'Stop'
if (-not [Environment]::UserInteractive) { throw 'An interactive Windows desktop is required.' }
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class EditorCaptureWin32 {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h,int cmd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h,int attr,out RECT rect,int size);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out RECT rect);
}
'@
[void][EditorCaptureWin32]::SetProcessDPIAware()
$directory = New-Item -ItemType Directory -Force -Path $OutputDirectory
$launched = $PSCmdlet.ParameterSetName -eq 'Launch'
$app = $null
$records = [System.Collections.Generic.List[object]]::new()
function Save-EditorWindow([string]$FileName, [IntPtr]$Handle) {
    if (-not [EditorCaptureWin32]::IsWindowVisible($Handle) -or [EditorCaptureWin32]::IsIconic($Handle)) {
        throw 'Editor window is hidden or minimized.'
    }
    $owner = [uint32]0
    [void][EditorCaptureWin32]::GetWindowThreadProcessId($Handle, [ref]$owner)
    if ($owner -ne $app.Id) { throw 'Window does not belong to the editor process.' }
    [void][EditorCaptureWin32]::SetForegroundWindow($Handle)
    Start-Sleep -Milliseconds 900
    if ([EditorCaptureWin32]::GetForegroundWindow() -ne $Handle) {
        throw 'Editor is not foreground; refusing to capture another window.'
    }
    $bounds = New-Object EditorCaptureWin32+RECT
    $size = [Runtime.InteropServices.Marshal]::SizeOf($bounds)
    if ([EditorCaptureWin32]::DwmGetWindowAttribute($Handle,9,[ref]$bounds,$size) -ne 0) {
        if (-not [EditorCaptureWin32]::GetWindowRect($Handle,[ref]$bounds)) { throw 'Cannot get window bounds.' }
    }
    $rect = [Drawing.Rectangle]::FromLTRB($bounds.Left,$bounds.Top,$bounds.Right,$bounds.Bottom)
    $screen = [Windows.Forms.SystemInformation]::VirtualScreen
    if (-not $screen.Contains($rect) -or $rect.Width -lt 640 -or $rect.Height -lt 400) {
        throw "Window bounds are outside the desktop or too small: $rect."
    }
    $bitmap = New-Object Drawing.Bitmap($rect.Width,$rect.Height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Location,[Drawing.Point]::Empty,$rect.Size)
        if ([EditorCaptureWin32]::GetForegroundWindow() -ne $Handle) { throw 'Foreground changed during capture.' }
        # Reject blank or nearly uniform frames; this is not visual acceptance.
        $colors = [System.Collections.Generic.HashSet[int]]::new()
        for ($y=0; $y -lt $bitmap.Height; $y+=12) {
            for ($x=0; $x -lt $bitmap.Width; $x+=12) { [void]$colors.Add($bitmap.GetPixel($x,$y).ToArgb()) }
        }
        if ($colors.Count -lt 16) { throw 'Capture appears blank or uniform.' }
        $file = Join-Path $directory.FullName ($FileName + '.png')
        $bitmap.Save($file,[Drawing.Imaging.ImageFormat]::Png)
        $records.Add([pscustomobject]@{
            file = ($FileName + '.png'); width = $rect.Width; height = $rect.Height;
            capturedAtUtc = [DateTime]::UtcNow.ToString('o'); commit = $Commit;
            sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash.ToLowerInvariant();
            source = 'actual foreground WinUI window'; workspace = 'not verified by capture script'
        })
        Write-Host "Captured $file"
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}
try {
    if ($launched) {
        $exe = (Resolve-Path -LiteralPath $Executable).Path
        if ([IO.Path]::GetFileName($exe) -ne 'MapStudio.Native.exe') { throw 'Expected MapStudio.Native.exe.' }
        $app = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -PassThru
    } else {
        $app = Get-Process -Id $ProcessId
        if ($app.ProcessName -ne 'MapStudio.Native') { throw 'Expected MapStudio.Native process.' }
    }
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        if ($app.HasExited) { throw "Editor exited with code $($app.ExitCode)." }
        $app.Refresh()
        $handle = $app.MainWindowHandle
        if ($handle -ne [IntPtr]::Zero -and $app.MainWindowTitle -like 'OMSI Map Studio*' -and $app.MainWindowTitle -notlike '*Native Preview*') { break }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($handle -eq [IntPtr]::Zero -or $app.MainWindowTitle -notlike 'OMSI Map Studio*' -or $app.MainWindowTitle -like '*Native Preview*') {
        throw 'Could not find the main editor window. Check startup.log.'
    }
    if ($launched) { [void][EditorCaptureWin32]::ShowWindowAsync($handle,3) }
    Start-Sleep -Seconds 3
    Save-EditorWindow $Name $handle
    if ($CaptureModules) {
        foreach ($module in @(@{ label='Road Studio'; file='01-road-studio-shell' },@{ label='Geo Studio'; file='02-geo-studio-shell' })) {
            try {
                $root = [Windows.Automation.AutomationElement]::FromHandle($handle)
                $condition = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty,$module.label)
                $control = $root.FindFirst([Windows.Automation.TreeScope]::Descendants,$condition)
                if ($null -eq $control -or -not $control.Current.IsEnabled -or $control.Current.IsOffscreen) { throw 'Module unavailable without a loaded workspace.' }
                $pattern = $null
                if (-not $control.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern,[ref]$pattern)) { throw 'Module does not expose InvokePattern.' }
                ([Windows.Automation.InvokePattern]$pattern).Invoke()
                Start-Sleep -Seconds 3
                Save-EditorWindow $module.file $handle
            } catch { Write-Warning "Optional module $($module.label): $_" }
        }
    }
} finally {
    if ($records.Count -gt 0) {
        ConvertTo-Json -InputObject ($records.ToArray()) -Depth 4 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $directory.FullName 'capture-manifest.json')
    }
    if ($launched -and $null -ne $app -and -not $app.HasExited) {
        [void]$app.CloseMainWindow()
        if (-not $app.WaitForExit(5000)) { Stop-Process -Id $app.Id -Force }
    }
}
