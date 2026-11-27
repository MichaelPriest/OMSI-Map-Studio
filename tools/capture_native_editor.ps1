# Capture the actual foreground WinUI window, never a mockup.
[CmdletBinding(DefaultParameterSetName='Launch')]
param(
    [Parameter(Mandatory=$true, ParameterSetName='Launch')][string]$Executable,
    [Parameter(Mandatory=$true, ParameterSetName='Attach')][int]$ProcessId,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9_-]{0,63}$')][string]$Name = '00-startup',
    [ValidateRange(5,120)][int]$TimeoutSeconds = 45,
    [string]$Commit = '',
    [switch]$CaptureModules,
    [switch]$CreateReviewMap
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
if ($CreateReviewMap -and (-not $launched -or $env:GITHUB_ACTIONS -ne 'true' -or -not $env:RUNNER_TEMP)) {
    throw 'Review map creation is restricted to a launched editor on a disposable GitHub Actions runner.'
}
$app = $null
$records = [System.Collections.Generic.List[object]]::new()
$reviewMapVerified = $false
$reviewMapName = $null
function Find-VisibleEditorElement([IntPtr]$Handle, [string]$Label, [int]$WaitSeconds = 15, [switch]$ById, [switch]$AllowDisabled) {
    $until = [DateTime]::UtcNow.AddSeconds($WaitSeconds)
    $property = if ($ById) { [Windows.Automation.AutomationElement]::AutomationIdProperty } else { [Windows.Automation.AutomationElement]::NameProperty }
    $condition = New-Object Windows.Automation.PropertyCondition($property,$Label)
    do {
        try {
            $root = [Windows.Automation.AutomationElement]::FromHandle($Handle)
            $matches = $root.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)
            for ($n=0; $n -lt $matches.Count; $n++) {
                $item = $matches.Item($n)
                if (-not $item.Current.IsOffscreen -and ($AllowDisabled -or $item.Current.IsEnabled)) { return $item }
            }
        } catch [Windows.Automation.ElementNotAvailableException] {}
        Start-Sleep -Milliseconds 350
    } while ([DateTime]::UtcNow -lt $until)
    return $null
}
function Invoke-EditorElement($Element, [string]$Label) {
    if ($null -eq $Element) { throw "Visible control not found: $Label" }
    $pattern = $null
    if (-not $Element.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern,[ref]$pattern)) { throw "InvokePattern unavailable: $Label" }
    ([Windows.Automation.InvokePattern]$pattern).Invoke()
}
function Set-EditorValue($Element, [string]$Value) {
    if ($null -eq $Element) { throw 'Review map input not found.' }
    $pattern = $null
    if (-not $Element.TryGetCurrentPattern([Windows.Automation.ValuePattern]::Pattern,[ref]$pattern)) { throw 'Review map input lacks ValuePattern.' }
    ([Windows.Automation.ValuePattern]$pattern).SetValue($Value)
}
function New-EditorReviewMap([IntPtr]$Handle) {
    $suffix = [Guid]::NewGuid().ToString('N').Substring(0,8)
    $folder = "UI_Review_$suffix"
    $display = "UI Review $suffix"
    Invoke-EditorElement (Find-VisibleEditorElement $Handle 'Novo mapa' 20) 'Novo mapa'
    Set-EditorValue (Find-VisibleEditorElement $Handle 'Pasta do projeto' 15) $folder
    Set-EditorValue (Find-VisibleEditorElement $Handle 'Nome do mapa' 15) $display
    Invoke-EditorElement (Find-VisibleEditorElement $Handle 'Criar e abrir' 15) 'Criar e abrir'
    $until = [DateTime]::UtcNow.AddSeconds(100)
    do {
        if ($app.HasExited) { throw 'Editor closed during test map creation.' }
        try {
            $status = Find-VisibleEditorElement $Handle 'StatusText' 1 -ById -AllowDisabled
            if ($null -ne $status -and $status.Current.Name -like "*$display*criado e aberto*") {
                return $display
            }
        } catch [Windows.Automation.ElementNotAvailableException] {}
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $until)
    throw 'Map creation was not confirmed by the editor status.'
}
function Save-EditorWindow([string]$FileName, [IntPtr]$Handle, [string]$Workspace = 'not verified by capture script', [string]$MapName = '') {
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
            source = 'actual foreground WinUI window'; workspace = $Workspace; mapName = $MapName
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
    if ($CreateReviewMap) {
        try {
            $reviewMapName = New-EditorReviewMap $handle
            $reviewMapVerified = $true
            Start-Sleep -Seconds 2
            Save-EditorWindow '03-loaded-map' $handle 'created and opened; editor status verified' $reviewMapName
        } catch {
            Write-Warning "Optional review map: $_"
            # If the creation dialog is still open, cancel only that dialog before module captures.
            $cancel = Find-VisibleEditorElement $handle 'Cancelar' 2
            if ($null -ne $cancel) { try { Invoke-EditorElement $cancel 'Cancelar' } catch {} }
        }
    }
    if ($CaptureModules) {
        $root = [Windows.Automation.AutomationElement]::FromHandle($handle)
        $welcomeCondition = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty,'Ir para o editor →')
        $welcome = $root.FindFirst([Windows.Automation.TreeScope]::Descendants,$welcomeCondition)
        if ($null -ne $welcome -and -not $welcome.Current.IsOffscreen -and $welcome.Current.IsEnabled) {
            $welcomePattern = $null
            if ($welcome.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern,[ref]$welcomePattern)) {
                ([Windows.Automation.InvokePattern]$welcomePattern).Invoke()
                Start-Sleep -Seconds 2
            }
        }
        foreach ($module in @(@{ label='Road Studio'; file='01-road-studio-shell' },@{ label='Geo Studio'; file='02-geo-studio-shell' })) {
            try {
                $root = [Windows.Automation.AutomationElement]::FromHandle($handle)
                $condition = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty,$module.label)
                $control = $null
                $candidates = $root.FindAll([Windows.Automation.TreeScope]::Descendants,$condition)
                for ($index = 0; $index -lt $candidates.Count; $index++) {
                    $candidate = $candidates.Item($index)
                    if ($candidate.Current.IsEnabled -and -not $candidate.Current.IsOffscreen) { $control = $candidate; break }
                }
                if ($null -eq $control -or -not $control.Current.IsEnabled -or $control.Current.IsOffscreen) { throw 'Module unavailable without a loaded workspace.' }
                $pattern = $null
                if (-not $control.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern,[ref]$pattern)) { throw 'Module does not expose InvokePattern.' }
                ([Windows.Automation.InvokePattern]$pattern).Invoke()
                Start-Sleep -Seconds 3
                if ($reviewMapVerified -and $module.label -eq 'Road Studio') {
                    # This verifies map creation, not road placement or a particular selected asset.
                    Save-EditorWindow '04-road-studio-loaded-map' $handle 'created and opened; editor status verified' $reviewMapName
                } else {
                    Save-EditorWindow $module.file $handle
                }
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
