@echo off
echo ========================================
echo ChromeGuard Task Scheduler Setup
echo ========================================
echo.
echo This will create a scheduled task for ChromeGuard to monitor Chrome every 3 minutes.
echo.
echo IMPORTANT: This script requires Administrator privileges!
echo.
echo If you get a PowerShell execution policy error, run this command first:
echo Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
echo.
pause

echo Starting PowerShell setup script...
powershell.exe -ExecutionPolicy Bypass -File "%~dp0setup-task-scheduler.ps1"

echo.
echo Setup script completed.
pause
