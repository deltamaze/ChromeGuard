@echo off
echo ========================================
echo ChromeGuard Deployment Script
echo ========================================
echo.

set SOURCE_DIR=%~dp0
set DEPLOY_DIR=C:\Main\ChromeGuard

echo Source Directory: %SOURCE_DIR%
echo Deploy Directory: %DEPLOY_DIR%
echo.

REM Check if deployment directory exists, create if not
if not exist "%DEPLOY_DIR%" (
    echo Creating deployment directory: %DEPLOY_DIR%
    mkdir "%DEPLOY_DIR%"
)

REM Check if source build directories exist
if not exist "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0" (
    echo ERROR: ChromeMonitor build directory not found!
    echo Please run 'dotnet build' in ChromeMonitor project first.
    pause
    exit /b 1
)

if not exist "%SOURCE_DIR%ChromeStarter\bin\Debug\net8.0" (
    echo ERROR: ChromeStarter build directory not found!
    echo Please run 'dotnet build' in ChromeStarter project first.
    pause
    exit /b 1
)

echo ========================================
echo Copying ChromeMonitor files...
echo ========================================

REM Copy ChromeMonitor executable and dependencies
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\ChromeMonitor.exe" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\ChromeMonitor.dll" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\ChromeMonitor.runtimeconfig.json" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\ChromeMonitor.deps.json" "%DEPLOY_DIR%\" /Y

REM Copy ChromeMonitor configuration and host files
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\appsettings.json" "%DEPLOY_DIR%\ChromeMonitor.appsettings.json" /Y
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\hosts_blocked.txt" "%DEPLOY_DIR%\" /Y

echo ========================================
echo Copying ChromeStarter files...
echo ========================================

REM Copy ChromeStarter executable and dependencies
copy "%SOURCE_DIR%ChromeStarter\bin\Debug\net8.0\ChromeStarter.exe" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeStarter\bin\Debug\net8.0\hosts_clean.txt" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeStarter\bin\Debug\net8.0\ChromeStarter.dll" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeStarter\bin\Debug\net8.0\ChromeStarter.runtimeconfig.json" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeStarter\bin\Debug\net8.0\ChromeStarter.deps.json" "%DEPLOY_DIR%\" /Y

REM Copy ChromeStarter configuration (with different name to avoid conflict)
copy "%SOURCE_DIR%ChromeStarter\bin\Debug\net8.0\appsettings.json" "%DEPLOY_DIR%\ChromeStarter.appsettings.json" /Y

echo ========================================
echo Copying shared dependencies...
echo ========================================

REM Copy Microsoft.Extensions dependencies (needed by both apps)
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\Microsoft.Extensions.Configuration.dll" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\Microsoft.Extensions.Configuration.Abstractions.dll" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\Microsoft.Extensions.Configuration.FileExtensions.dll" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\Microsoft.Extensions.Configuration.Json.dll" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\Microsoft.Extensions.FileProviders.Abstractions.dll" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\Microsoft.Extensions.FileProviders.Physical.dll" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\Microsoft.Extensions.FileSystemGlobbing.dll" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\Microsoft.Extensions.Primitives.dll" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\System.IO.Pipelines.dll" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\System.Text.Encodings.Web.dll" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\System.Text.Json.dll" "%DEPLOY_DIR%\" /Y

REM Copy runtimes folder if it exists
if exist "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\runtimes" (
    echo Copying runtimes folder...
    xcopy "%SOURCE_DIR%ChromeMonitor\bin\Debug\net8.0\runtimes" "%DEPLOY_DIR%\runtimes\" /E /I /Y
)

echo ========================================
echo Copying documentation files...
echo ========================================

REM Copy documentation
copy "%SOURCE_DIR%README.md" "%DEPLOY_DIR%\" /Y
copy "%SOURCE_DIR%TASK_SCHEDULER_SETUP.md" "%DEPLOY_DIR%\" /Y

echo ========================================
echo Creating configuration files...
echo ========================================

REM Create unified appsettings.json for the deployment directory
echo Creating unified appsettings.json...
(
echo {
echo   "AppSettings": {
echo     "SessionLogPath": "C:\\Main\\ChromeGuard\\chrome_sessions.log",
echo     "SystemHostsPath": "C:\\Windows\\System32\\drivers\\etc\\hosts",
echo     "CleanHostsTemplatePath": "hosts_clean.txt",
echo     "BlockedHostsTemplatePath": "hosts_blocked.txt",
echo     "WarningTimeoutSeconds": 55,
echo     "ChromeProcessName": "chrome"
echo   }
echo }
) > "%DEPLOY_DIR%\appsettings.json"

echo ========================================
echo Creating batch helpers...
echo ========================================

REM Create helper batch file to run ChromeStarter
(
echo @echo off
echo echo Starting ChromeStarter...
echo echo.
echo echo NOTE: This application requires administrator privileges
echo echo to modify the hosts file and will prompt accordingly.
echo echo.
echo pause
echo "%~dp0ChromeStarter.exe"
echo pause
) > "%DEPLOY_DIR%\StartChromeStarter.bat"

REM Create helper batch file to test ChromeMonitor
(
echo @echo off
echo echo Testing ChromeMonitor...
echo echo.
echo echo NOTE: This application requires administrator privileges.
echo echo Run this Command Prompt as Administrator for full functionality.
echo echo.
echo pause
echo "%~dp0ChromeMonitor.exe"
echo pause
) > "%DEPLOY_DIR%\TestChromeMonitor.bat"

echo ========================================
echo Deployment Summary
echo ========================================
echo.
echo Deployed files to: %DEPLOY_DIR%
echo.
echo Main Executables:
echo   - ChromeMonitor.exe    ^(For Task Scheduler^)
echo   - ChromeStarter.exe    ^(Manual session start^)
echo.
echo Helper Scripts:
echo   - StartChromeStarter.bat    ^(Easy ChromeStarter launch^)
echo   - TestChromeMonitor.bat     ^(Test ChromeMonitor^)
echo.
echo Configuration:
echo   - appsettings.json     ^(Unified configuration^)
echo   - hosts_blocked.txt    ^(Blocked sites template^)
echo   - hosts_clean.txt      ^(Clean hosts template^)
echo.
echo Documentation:
echo   - README.md
echo   - TASK_SCHEDULER_SETUP.md
echo.
echo Session Log Location:
echo   - C:\Main\ChromeGuard\chrome_sessions.log
echo.
echo ========================================
echo Deployment completed successfully!
echo ========================================
echo.
echo Next Steps:
echo 1. Run 'StartChromeStarter.bat' as Administrator to test session creation
echo 2. Run 'TestChromeMonitor.bat' as Administrator to test monitoring
echo 3. Set up Task Scheduler using TASK_SCHEDULER_SETUP.md instructions
echo    ^(Point to: %DEPLOY_DIR%\ChromeMonitor.exe^)
echo.
pause
