$ErrorActionPreference = 'Stop'
try {
    $destination = Join-Path $env:LOCALAPPDATA 'AwakeToggleClean'
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $exe = Join-Path $destination 'AwakeToggle.exe'
    # Stop the current version if installed and active, restoring previous timeouts.
    if (Test-Path $exe) {
        & $exe
        for ($i = 0; $i -lt 60; $i++) {
            $running = Get-CimInstance Win32_Process -Filter "name='AwakeToggle.exe'" -ErrorAction SilentlyContinue |
                Where-Object { $_.ExecutablePath -eq $exe }
            if (-not $running) { break }
            Start-Sleep -Milliseconds 100
        }
        if ($running) { throw 'Close Awake Toggle from the tray first, then run Install.cmd again.' }
        Remove-Item $exe -Force
    }

    # Windows PowerShell 5.1 can compile a GUI executable using the .NET Framework included in Windows 11.
    Add-Type -Path (Join-Path $PSScriptRoot 'AwakeToggle.cs') `
        -OutputAssembly $exe -OutputType WindowsApplication `
        -ReferencedAssemblies 'System.dll', 'System.Core.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll' `
        -ErrorAction Stop

    $desktop = [Environment]::GetFolderPath('DesktopDirectory')
    $shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $desktop 'Awake Toggle.lnk'))
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $destination
    $shortcut.Description = 'Toggle awake mode and restore original sleep settings'
    $shortcut.IconLocation = "$exe,0"
    $shortcut.Save()
    Write-Host ''
    Write-Host 'Installation successful!' -ForegroundColor Green
    Write-Host "Desktop shortcut: $desktop\Awake Toggle.lnk"
    Write-Host 'Double-click it to turn awake mode ON; double-click again to turn it OFF.'
}
catch {
    Write-Host ''
    Write-Host ('Installation failed: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
