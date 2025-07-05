@echo off
echo ========================================
echo ChromeGuard Monitor Logs
echo ========================================
echo.

set LOG_FILE=C:\Main\ChromeGuard\ChromeMonitor.log

if not exist "%LOG_FILE%" (
    echo Log file not found: %LOG_FILE%
    echo.
    echo The task may not have run yet, or logging is not configured.
    echo Try running the task manually first.
    echo.
    pause
    exit /b 1
)

echo Displaying last 50 lines of ChromeMonitor.log
echo Press Ctrl+C to stop monitoring, or close this window
echo.
echo ========================================
echo.

REM Show the last 50 lines and then follow the file for new content
powershell.exe -Command "Get-Content '%LOG_FILE%' -Tail 50 -Wait"
