@echo off
REM Build script for Electrical Component Sandbox

echo ====================================
echo Electrical Component Sandbox Build
echo ====================================
echo.

REM Check if dotnet is installed
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK not found. Please install .NET 8.0 SDK or later.
    echo Download from: https://dotnet.microsoft.com/download
    exit /b 1
)

echo Step 1: Restoring NuGet packages...
dotnet restore ElectricalComponentSandbox\ElectricalComponentSandbox.csproj
if %errorlevel% neq 0 (
    echo ERROR: Package restore failed.
    exit /b 1
)
echo.

echo Step 2: Building project...
dotnet build ElectricalComponentSandbox\ElectricalComponentSandbox.csproj -c Release
if %errorlevel% neq 0 (
    echo ERROR: Build failed.
    exit /b 1
)
echo.

echo ====================================
echo Build completed successfully!
echo ====================================
echo.
echo To run the application:
echo   dotnet run --project ElectricalComponentSandbox\ElectricalComponentSandbox.csproj
echo.
echo Or navigate to:
echo   ElectricalComponentSandbox\bin\Release\net8.0-windows\
echo   and run ElectricalComponentSandbox.exe
echo.

pause
