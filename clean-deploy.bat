@echo off
echo ========================================
echo ChromeGuard Clean Deploy Script
echo ========================================
echo.

set DEPLOY_DIR=C:\Main\ChromeGuard

echo This will clean the deployment directory and redeploy all files.
echo Deploy Directory: %DEPLOY_DIR%
echo.
echo WARNING: This will delete all files in the deployment directory!
echo          ^(Except chrome_sessions.log if it exists^)
echo.
set /p CONFIRM="Are you sure you want to continue? (Y/N): "
if /i not "%CONFIRM%"=="Y" (
    echo Deployment cancelled.
    pause
    exit /b 0
)

echo.
echo Backing up session log if it exists...
if exist "%DEPLOY_DIR%\chrome_sessions.log" (
    copy "%DEPLOY_DIR%\chrome_sessions.log" "%TEMP%\chrome_sessions_backup.log" /Y
    echo Session log backed up to: %TEMP%\chrome_sessions_backup.log
)

echo.
echo Cleaning deployment directory...
if exist "%DEPLOY_DIR%" (
    rd /s /q "%DEPLOY_DIR%"
)
mkdir "%DEPLOY_DIR%"

echo.
echo Restoring session log if it was backed up...
if exist "%TEMP%\chrome_sessions_backup.log" (
    copy "%TEMP%\chrome_sessions_backup.log" "%DEPLOY_DIR%\chrome_sessions.log" /Y
    del "%TEMP%\chrome_sessions_backup.log"
    echo Session log restored.
)

echo.
echo Starting fresh deployment...
call "%~dp0deploy.bat"
