@echo off
REM Creates a desktop shortcut for Electrical Component Sandbox
REM Run this script after building the project

set "EXE_DIR=%~dp0ElectricalComponentSandbox\bin\Release\net8.0-windows"
set "EXE_PATH=%EXE_DIR%\ElectricalComponentSandbox.exe"
set "SHORTCUT_PATH=%USERPROFILE%\Desktop\Electrical Component Sandbox.lnk"

if not exist "%EXE_PATH%" (
    echo ERROR: Application not found at %EXE_PATH%
    echo Please build the project first by running: build.bat
    pause
    exit /b 1
)

echo Creating desktop shortcut...

powershell -NoProfile -Command ^
    "$ws = New-Object -ComObject WScript.Shell; ^
     $sc = $ws.CreateShortcut('%SHORTCUT_PATH%'); ^
     $sc.TargetPath = '%EXE_PATH%'; ^
     $sc.WorkingDirectory = '%EXE_DIR%'; ^
     $sc.Description = 'Electrical Component Sandbox - Parametric Electrical Component Designer'; ^
     $sc.IconLocation = '%EXE_PATH%,0'; ^
     $sc.Save()"

if %errorlevel% equ 0 (
    echo.
    echo Desktop shortcut created successfully!
    echo Location: %SHORTCUT_PATH%
) else (
    echo ERROR: Failed to create shortcut.
)

echo.
pause
