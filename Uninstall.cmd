@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0UninstallPackage.ps1"
if errorlevel 1 pause
