@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo ============================================
echo  TrafficLight Build Script
echo ============================================
echo.

:: Clean old build artifacts
echo [1/3] Cleaning old build files...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
echo       Done
echo.

:: Build
echo [2/3] Publishing...
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:PublishReadyToRun=false ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Build failed. Make sure .NET 10 SDK is installed.
    pause
    exit /b 1
)
echo       Build successful
echo.

:: UPX compression (if installed)
echo [3/3] Checking UPX...
where upx >nul 2>nul
if %ERRORLEVEL% equ 0 (
    echo       UPX found, compressing...
    upx --best --force "bin\Release\net10.0-windows\win-x64\publish\TrafficLight.exe"
    echo       Compression done
) else (
    echo       UPX not found, skipping compression
    echo       Download: https://github.com/upx/upx/releases
)

echo.
echo ============================================
echo  Build complete!
echo  Output: bin\Release\net10.0-windows\win-x64\publish\TrafficLight.exe
echo ============================================

pause
