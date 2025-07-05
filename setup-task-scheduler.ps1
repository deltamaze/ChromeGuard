# ChromeGuard Task Scheduler Setup Script
# This script creates a scheduled task for ChromeMonitor to run every 3 minutes

param(
    [string]$ChromeGuardPath = "C:\Main\ChromeGuard"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ChromeGuard Task Scheduler Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Please right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

# Verify ChromeMonitor.exe exists
$chromeMonitorPath = Join-Path $ChromeGuardPath "ChromeMonitor.exe"
if (-not (Test-Path $chromeMonitorPath)) {
    Write-Host "ERROR: ChromeMonitor.exe not found at: $chromeMonitorPath" -ForegroundColor Red
    Write-Host "Please ensure you have deployed ChromeGuard to the correct location." -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "ChromeMonitor found at: $chromeMonitorPath" -ForegroundColor Green
Write-Host ""

# Task configuration
$taskName = "ChromeGuard Monitor"
$taskDescription = "Monitors Chrome usage and enforces browsing limits every 3 minutes"

# Check if task already exists
try {
    $existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction Stop
    Write-Host "WARNING: Task '$taskName' already exists!" -ForegroundColor Yellow
    $response = Read-Host "Do you want to replace it? (Y/N)"
    if ($response -ne "Y" -and $response -ne "y") {
        Write-Host "Task setup cancelled." -ForegroundColor Yellow
        Read-Host "Press Enter to exit"
        exit 0
    }
    
    Write-Host "Removing existing task..." -ForegroundColor Yellow
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    Write-Host "Existing task removed." -ForegroundColor Green
}
catch {
    Write-Host "No existing task found. Creating new task..." -ForegroundColor Green
}

Write-Host ""
Write-Host "Creating scheduled task with the following configuration:" -ForegroundColor Cyan
Write-Host "  Name: $taskName" -ForegroundColor White
Write-Host "  Frequency: Every 3 minutes" -ForegroundColor White
Write-Host "  Privileges: Highest (Administrator)" -ForegroundColor White
Write-Host "  Visibility: Hidden" -ForegroundColor White
Write-Host "  Executable: $chromeMonitorPath" -ForegroundColor White
Write-Host ""

try {
    # Create the action (what the task will do)
    # Run the exe directly since it's now a WinExe (no console window)
    $action = New-ScheduledTaskAction -Execute $chromeMonitorPath -WorkingDirectory $ChromeGuardPath

    # Create the trigger (when the task will run)
    # Fix: Use a more reasonable duration instead of MaxValue
    $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 3) -RepetitionDuration (New-TimeSpan -Days 3650)

    # Create task settings
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -RunOnlyIfNetworkAvailable:$false -DontStopOnIdleEnd -ExecutionTimeLimit (New-TimeSpan -Minutes 2) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -Hidden

    # Set the task to run with highest privileges but under current user (not SYSTEM)
    # This allows interaction with user session (seeing Chrome processes, showing message boxes)
    $currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    $principal = New-ScheduledTaskPrincipal -UserId $currentUser -LogonType Interactive -RunLevel Highest

    # Register the scheduled task
    $task = Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description $taskDescription

    Write-Host "SUCCESS: Scheduled task created successfully!" -ForegroundColor Green
    Write-Host ""
    
    # Display task information
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Task Configuration Summary" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Task Name: $($task.TaskName)" -ForegroundColor White
    Write-Host "State: $($task.State)" -ForegroundColor White
    Write-Host "Next Run Time: $((Get-ScheduledTask -TaskName $taskName | Get-ScheduledTaskInfo).NextRunTime)" -ForegroundColor White
    Write-Host "Run As: $currentUser (Administrator)" -ForegroundColor White
    Write-Host "Frequency: Every 3 minutes" -ForegroundColor White
    Write-Host "Hidden: Yes (No console window)" -ForegroundColor White
    Write-Host "Log File: $ChromeGuardPath\ChromeMonitor.log" -ForegroundColor White
    Write-Host ""
    
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Testing the Task" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    $testResponse = Read-Host "Would you like to test the task now? (Y/N)"
    if ($testResponse -eq "Y" -or $testResponse -eq "y") {
        Write-Host "Running task manually for testing..." -ForegroundColor Yellow
        Start-ScheduledTask -TaskName $taskName
        
        Start-Sleep -Seconds 3
        
        $taskInfo = Get-ScheduledTask -TaskName $taskName | Get-ScheduledTaskInfo
        Write-Host "Last Run Time: $($taskInfo.LastRunTime)" -ForegroundColor White
        Write-Host "Last Task Result: $($taskInfo.LastTaskResult)" -ForegroundColor White
        
        if ($taskInfo.LastTaskResult -eq 0) {
            Write-Host "Task test completed successfully!" -ForegroundColor Green
        } else {
            Write-Host "Task may have encountered an issue. Check Task Scheduler for details." -ForegroundColor Yellow
        }
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Setup Complete!" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "ChromeGuard is now monitoring Chrome every 3 minutes." -ForegroundColor Green
    Write-Host ""
    Write-Host "Console Output:" -ForegroundColor Cyan
    Write-Host "  All console output is logged to: $ChromeGuardPath\ChromeMonitor.log" -ForegroundColor White
    Write-Host "  Use this file to debug any issues with the monitoring task." -ForegroundColor White
    Write-Host ""
    Write-Host "Management Commands:" -ForegroundColor Cyan
    Write-Host "  View task:     Get-ScheduledTask -TaskName '$taskName'" -ForegroundColor White
    Write-Host "  Start task:    Start-ScheduledTask -TaskName '$taskName'" -ForegroundColor White
    Write-Host "  Stop task:     Stop-ScheduledTask -TaskName '$taskName'" -ForegroundColor White
    Write-Host "  Remove task:   Unregister-ScheduledTask -TaskName '$taskName'" -ForegroundColor White
    Write-Host ""
    Write-Host "You can also manage the task through Task Scheduler (taskschd.msc)" -ForegroundColor White
    Write-Host ""
}
catch {
    Write-Host "ERROR: Failed to create scheduled task!" -ForegroundColor Red
    Write-Host "Error details: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
}

Read-Host "Press Enter to exit"
