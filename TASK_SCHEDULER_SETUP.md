# ChromeGuard Task Scheduler Setup

## Overview
ChromeMonitor is designed to run as a Windows Task Scheduler job every 5 minutes. It can run in the background without showing a console window, but will still display warning messages to the user when needed.

## Task Scheduler Configuration

### 1. Create New Task
1. Open Task Scheduler (taskschd.msc)
2. Click "Create Task..." (not "Create Basic Task")
3. Name: "ChromeGuard Monitor"
4. Description: "Monitors Chrome usage and enforces browsing limits"

### 2. General Settings
- **Security options:**
  - ✅ Run whether user is logged on or not
  - ✅ Run with highest privileges (Required for hosts file modification)
  - ✅ Hidden (This prevents the console window from appearing)
- **Configure for:** Windows 10/11

### 3. Triggers
- **Begin the task:** On a schedule
- **Settings:** Daily
- **Advanced settings:**
  - ✅ Repeat task every: 5 minutes
  - ✅ for a duration of: Indefinitely
  - ✅ Enabled

### 4. Actions
- **Action:** Start a program
- **Program/script:** Full path to ChromeMonitor.exe
  ```
  C:\path\to\ChromeGuard\ChromeMonitor\bin\Debug\net8.0\ChromeMonitor.exe
  ```
- **Start in:** Directory containing the executable
  ```
  C:\path\to\ChromeGuard\ChromeMonitor\bin\Debug\net8.0\
  ```

### 5. Conditions
- **Power:**
  - ✅ Wake the computer to run this task
  - ❌ Start the task only if the computer is on AC power
  - ❌ Stop if the computer switches to battery power

### 6. Settings
- ✅ Allow task to be run on demand
- ✅ Run task as soon as possible after a scheduled start is missed
- ❌ Stop the task if it runs longer than: (Leave unchecked)
- **If the running task does not end when requested:**
  - ○ Do not start a new instance

## How the Warning System Works

### Background Execution
- The task runs **hidden** in the background (no console window)
- Console output is logged to Task Scheduler history
- The application completes its monitoring cycle and exits

### Warning Messages
When Chrome is running without a valid session:

1. **Native Windows MessageBox** appears using P/Invoke
   - Uses `MB_TOPMOST | MB_SYSTEMMODAL` flags
   - Appears even when the main process is hidden
   - Shows on top of all other windows
   - Cannot be missed by the user

2. **Message Content:**
   ```
   Chrome will shut down in 1 minute.
   
   Re-run ChromeStarter.exe to extend your session if needed.
   ```

3. **User Response Options:**
   - **Click OK:** Acknowledges the warning, 55-second countdown continues
   - **Run ChromeStarter.exe:** Creates new session, prevents Chrome closure
   - **Ignore:** Chrome will be closed after 55 seconds

### Process Flow
```
Task Scheduler (every 5 minutes)
↓
ChromeMonitor.exe (hidden)
↓
Check for valid session
↓
If Chrome running + No session:
  → Show MessageBox (visible to user)
  → Wait 55 seconds
  → Re-check session
  → Close Chrome if still no session
↓
Reset hosts file to blocked mode
↓
Exit (task completes)
```

## Benefits of This Approach

1. **No Console Clutter:** Runs silently in background
2. **Reliable Warnings:** Native MessageBox always visible to user
3. **System Integration:** Proper Windows service-like behavior
4. **Audit Trail:** Task Scheduler provides execution history
5. **Robust:** Continues working even if user is not actively logged in

## Troubleshooting

### Check Task History
1. Open Task Scheduler
2. Find "ChromeGuard Monitor" task
3. Click "History" tab to see execution logs

### Common Issues
- **Task fails to start:** Check "Run with highest privileges" is enabled
- **Hosts file not updating:** Verify administrator privileges
- **Warning not showing:** Ensure task is not running in "Background only" mode

### Manual Testing
Run the executable manually as administrator to test functionality:
```cmd
# Run in Command Prompt as Administrator
cd "C:\path\to\ChromeGuard\ChromeMonitor\bin\Debug\net8.0"
ChromeMonitor.exe
```
