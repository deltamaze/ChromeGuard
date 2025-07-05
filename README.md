Distraction-Free Chrome Management (Revised)

This document outlines a system designed to help you manage Chrome usage and minimize distractions by enforcing website blocks and controlled Browse sessions, now powered by C# applications.

### 1. Project Goal

The primary goal is to prevent mindless Browse by automatically closing Chrome and requiring explicit permission and a time limit for its use, all while leveraging your existing host file blocks. A new warning system will provide a grace period before closure.

### 2. Core Components

- **Host File:** Your existing `hosts` file, modified to block distracting websites.
    
- **`chrome-monitor.exe` (C# Application):** A C# executable added to Windows Task Scheduler, running every 5 minutes, responsible for monitoring Chrome, issuing warnings, enforcing closure, and resetting your `hosts` file.
    
- **`start-chrome.exe` (C# Application):** A C# executable you will manually run to initiate controlled Chrome sessions.
    
- **Session Log File:** A text file to record details of your planned Chrome usage.
    

### 3. Detailed Design

#### 3.1. Automated Chrome Closure & Host File Reset (`chrome-monitor.exe`)

- **Mechanism:** The `chrome-monitor.exe` C# application will be configured to run every 5 minutes via Windows Task Scheduler. It will require administrator privileges to modify the `hosts` file and manage Chrome processes.
    
- **Logic:**
    
    1. **Check for Active Session:** The application will read the **session log file** to determine if an active Chrome session is currently permitted and within its time limit.
        
    2. **Detect Chrome & Issue Warning (if no active session or expired):**
        
        - The application will check for any running `chrome.exe` processes.
            
        - If `chrome.exe` is found **AND** no valid active session is recorded in the log file (or the existing session has expired), the application will:
            
            - Display a prominent **message box pop-up** on the screen (e.g., "Chrome will shut down in 1 minute. Re-run start-chrome.exe to extend your session if needed.") This message box should ideally be "always on top" for visibility.
                
            - Pause its execution for approximately **50-55 seconds** to allow the user to see the message and react.
                
    3. **Final Check & Close Chrome:**
        
        - After the warning pause, `chrome-monitor.exe` will perform a **final re-check of the session log file**.
            
        - If, after this re-check, there is _still no active valid session_ (meaning the user did not run `start-chrome.exe` to extend their time during the warning period), the application will forcefully close all `chrome.exe` processes.
            
    4. **Reset Host File:** Regardless of Chrome's status, `chrome-monitor.exe` will then overwrite your `hosts` file with the **prevention mode** version (all distracting websites blocked). This ensures your blocks are always active unless a specific Browse session is initiated via `start-chrome.exe`.
        

#### 3.2. Controlled Chrome Session Initiation (`start-chrome.exe`)

- **Trigger:** You will manually execute the `start-chrome.exe` C# application when you genuinely need to use Chrome. This application will also require administrator privileges to modify the `hosts` file.
    
- **User Interaction (Console or Simple UI):** The application will present prompts to the user:
    
    1. **Host File Configuration:**
        
        - "Do you need Chrome with **all pages** (no blocks) or **limited pages** (with blocks)?" (User inputs 'A' for All or 'L' for Limited).
            
        - If "all pages" is selected, the script will temporarily modify your `hosts` file to remove all blocked entries.
            
    2. **Session Duration:**
        
        - "How many minutes do you need to use Chrome?" (e.g., 15, 30, 60 - User inputs a number). Input validation should ensure a positive integer.
            
    3. **Reason for Use:**
        
        - "Please provide a brief reason for using Chrome:" (User inputs text).
            
- **Session Log File Update:**
    
    - The application will record the following information in the **session log file**:
        
        - Timestamp of session start.
            
        - Chosen host file mode (all pages/limited pages).
            
        - Requested duration (in minutes).
            
        - Reason for use.
            
        - Calculated session end time.
            
- **Launch Chrome:** After recording the session details and configuring the `hosts` file, the application will launch Chrome.
    

### 4. Session Log File Structure

The log file will be a simple text file (e.g., `chrome_sessions.log`) with each line representing a session:

```
YYYY-MM-DD HH:MM:SS | ALL/LIMITED | DURATION_MINUTES | END_TIME_HH:MM:SS | REASON
```

**Example:**

```
2025-07-04 09:30:00 | ALL | 30 | 09:59:59 | Checking work email
2025-07-04 10:15:00 | LIMITED | 10 | 10:24:59 | Quick tech news check
```

### 5. Benefits

- **Enhanced Distraction Reduction:** Forceful Chrome closures and the explicit session initiation prevent aimless Browse.
    
- **Improved User Experience:** The 1-minute warning provides a crucial grace period, allowing for session extension without abrupt closures.
    
- **Mindful Usage:** Requires conscious decision-making and a stated purpose for using Chrome.
    
- **Accountability:** Logging sessions provides a clear record of your Chrome usage.
    
- **Robust Host File Integration:** Leverages existing blocking mechanisms for fine-grained control, managed programmatically.
    
- **Native Windows Integration:** C# applications and Task Scheduler provide a more integrated and stable solution for Windows users compared to batch scripts or external cron tools.