#!/bin/bash
# Build script for Electrical Component Sandbox (Linux/Mac - cross-platform build only)

echo "===================================="
echo "Electrical Component Sandbox Build"
echo "===================================="
echo ""

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK not found. Please install .NET 8.0 SDK or later."
    echo "Download from: https://dotnet.microsoft.com/download"
    exit 1
fi

echo "Step 1: Restoring NuGet packages..."
dotnet restore ElectricalComponentSandbox/ElectricalComponentSandbox.csproj
if [ $? -ne 0 ]; then
    echo "ERROR: Package restore failed."
    exit 1
fi
echo ""

echo "Step 2: Building project (cross-platform)..."
dotnet build ElectricalComponentSandbox/ElectricalComponentSandbox.csproj -c Release
if [ $? -ne 0 ]; then
    echo "ERROR: Build failed."
    exit 1
fi
echo ""

echo "===================================="
echo "Build completed successfully!"
echo "===================================="
echo ""
echo "Note: This is a WPF application and requires Windows to run."
echo "The build process creates the binaries for Windows targets."
echo ""
echo "To run on Windows:"
echo "  dotnet run --project ElectricalComponentSandbox/ElectricalComponentSandbox.csproj"
echo ""
