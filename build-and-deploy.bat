@echo off
echo ========================================
echo ChromeGuard Build and Deploy Script
echo ========================================
echo.

set SOURCE_DIR=%~dp0

echo Building ChromeMonitor...
cd /d "%SOURCE_DIR%ChromeMonitor"
dotnet build
if %errorlevel% neq 0 (
    echo ERROR: ChromeMonitor build failed!
    pause
    exit /b 1
)

echo.
echo Building ChromeStarter...
cd /d "%SOURCE_DIR%ChromeStarter"
dotnet build
if %errorlevel% neq 0 (
    echo ERROR: ChromeStarter build failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo Build completed successfully!
echo ========================================
echo.
echo Starting deployment...
echo.

cd /d "%SOURCE_DIR%"
call deploy.bat
