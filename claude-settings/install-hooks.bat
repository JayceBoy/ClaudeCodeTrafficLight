@echo off
REM Install Claude Code hooks from this project to global config.
REM Just double-click this file to run.

cd /d "%~dp0"
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "install-hooks.ps1"
pause
