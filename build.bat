@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo ============================================
echo  TrafficLight 一键构建脚本
echo ============================================
echo.

:: 清理旧的构建文件
echo [1/3] 清理旧构建文件...
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
echo      完成
echo.

:: 构建
echo [2/3] 构建发布包...
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:PublishReadyToRun=false ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none

if %ERRORLEVEL% neq 0 (
    echo.
    echo [错误] 构建失败，请检查是否安装了 .NET 10 SDK
    pause
    exit /b 1
)
echo      构建成功
echo.

:: UPX 压缩（如果安装了 UPX）
echo [3/3] 正在检查 UPX...
where upx >nul 2>nul
if %ERRORLEVEL% equ 0 (
    echo      发现 UPX，正在压缩...
    upx --best --force "bin\Release\net10.0-windows\win-x64\publish\TrafficLight.exe"
    echo      压缩完成
) else (
    echo      未安装 UPX，跳过压缩步骤
    echo      如需压缩可安装: https://github.com/upx/upx/releases
)

echo.
echo ============================================
echo  构建完成！
echo  产物: bin\Release\net10.0-windows\win-x64\publish\TrafficLight.exe
echo ============================================

pause
