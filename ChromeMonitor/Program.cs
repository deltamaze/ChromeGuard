using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Security.Principal;

namespace ChromeMonitor
{
    class Program
    {
        private static IConfiguration? _configuration;
        private static string _sessionLogPath = "";
        private static string _systemHostsPath = "";
        private static string _blockedHostsTemplatePath = "";
        private static string _chromeProcessName = "chrome";
        private static string _logFilePath = "";

        static async Task Main(string[] args)
        {
            try
            {
                // Initialize logging first
                _logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "ChromeMonitor.log");
                
                await LogMessage($"ChromeMonitor - Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
                // Load configuration
                await LoadConfiguration();

                // Check if running as administrator
                if (!IsRunAsAdministrator())
                {
                    await LogMessage("ERROR: Not running as administrator!");
                    await LogMessage("ChromeMonitor requires administrator privileges to modify hosts file and manage processes.");
                    Environment.Exit(1);
                }

                // Main monitoring logic
                await PerformMonitoringCycle();
                
                await LogMessage($"ChromeMonitor - Completed at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                await LogMessage($"FATAL ERROR: {ex.Message}");
                await LogMessage($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }

        private static async Task LogMessage(string message)
        {
            try
            {
                var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
                
                // Write to log file only - no console output for headless operation
                await File.AppendAllTextAsync(_logFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // If logging fails, we can't do much about it in a headless app
                // Just silently continue to avoid any potential console window appearance
            }
        }

        private static async Task LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            _configuration = builder.Build();

            _sessionLogPath = _configuration["AppSettings:SessionLogPath"] ?? "";
            _systemHostsPath = _configuration["AppSettings:SystemHostsPath"] ?? "";
            
            _blockedHostsTemplatePath = _configuration["AppSettings:BlockedHostsTemplatePath"] ?? "";
            _chromeProcessName = _configuration["AppSettings:ChromeProcessName"] ?? "chrome";

            await LogMessage("Configuration loaded successfully.");
        }

        private static bool IsRunAsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static async Task PerformMonitoringCycle()
        {
            await LogMessage("Starting monitoring cycle...");

            // Step 1: Check for active session
            var activeSession = await CheckForActiveSession();
            await LogMessage($"Active session check: {(activeSession != null ? "Found valid session" : "No valid session")}");

            // Step 2: Check for Chrome processes
            var chromeProcesses = await GetChromeProcesses();
            await LogMessage($"Chrome processes found: {chromeProcesses.Length}");

            // Step 3: Handle Chrome closure if needed
            if (chromeProcesses.Length > 0 && activeSession == null)
            {
                await LogMessage("Chrome is running without valid session - closing immediately");
                await CloseChromeProcesses();
            }
            else if (chromeProcesses.Length > 0 && activeSession != null)
            {
                await LogMessage($"Chrome is running with valid session (expires at {activeSession.EndTime:HH:mm:ss})");
            }
            else
            {
                await LogMessage("No Chrome processes found");
            }

            // Step 4: Reset hosts file only if no active session
            if (activeSession == null)
            {
                await LogMessage("No active session - resetting hosts file to blocked mode");
                await ResetHostsFile();
            }
            else
            {
                await LogMessage("Active session found - leaving hosts file unchanged");
            }
        }

        private static async Task<SessionInfo?> CheckForActiveSession()
        {
            try
            {
                if (!File.Exists(_sessionLogPath))
                {
                    await LogMessage("Session log file does not exist");
                    return null;
                }

                var lines = await File.ReadAllLinesAsync(_sessionLogPath);
                if (lines.Length == 0)
                {
                    await LogMessage("Session log file is empty");
                    return null;
                }

                // Get the most recent session (last line)
                var lastLine = lines[^1];
                var session = await ParseSessionLine(lastLine);
                
                if (session == null)
                {
                    await LogMessage("Failed to parse most recent session");
                    return null;
                }

                // Check if session is still active
                if (DateTime.Now <= session.EndTime)
                {
                    await LogMessage($"Found active session: {session.Mode}, expires at {session.EndTime:HH:mm:ss}, reason: {session.Reason}");
                    return session;
                }
                else
                {
                    await LogMessage($"Most recent session expired at {session.EndTime:HH:mm:ss}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                await LogMessage($"Error checking for active session: {ex.Message}");
                return null;
            }
        }

        private static async Task<SessionInfo?> ParseSessionLine(string line)
        {
            try
            {
                // Expected format: YYYY-MM-DD HH:MM:SS | ALL/LIMITED | DURATION_MINUTES | END_TIME_HH:MM:SS | REASON
                var parts = line.Split('|').Select(p => p.Trim()).ToArray();
                
                if (parts.Length != 5)
                {
                    await LogMessage($"Invalid session line format: {line}");
                    return null;
                }

                var startTime = DateTime.Parse(parts[0]);
                var mode = parts[1];
                var durationMinutes = int.Parse(parts[2]);
                var endTimeStr = parts[3];
                var reason = parts[4];

                // Parse end time - it's in HH:mm:ss format, so combine with start date
                var endTime = DateTime.Parse($"{startTime:yyyy-MM-dd} {endTimeStr}");
                
                // Handle case where end time is next day
                if (endTime < startTime)
                {
                    endTime = endTime.AddDays(1);
                }

                return new SessionInfo
                {
                    StartTime = startTime,
                    EndTime = endTime,
                    Mode = mode,
                    DurationMinutes = durationMinutes,
                    Reason = reason
                };
            }
            catch (Exception ex)
            {
                await LogMessage($"Error parsing session line '{line}': {ex.Message}");
                return null;
            }
        }

        private static async Task<Process[]> GetChromeProcesses()
        {
            try
            {
                return Process.GetProcessesByName(_chromeProcessName);
            }
            catch (Exception ex)
            {
                await LogMessage($"Error getting Chrome processes: {ex.Message}");
                return Array.Empty<Process>();
            }
        }

        private static async Task CloseChromeProcesses()
        {
            try
            {
                var processes = await GetChromeProcesses();
                await LogMessage($"Attempting to close {processes.Length} Chrome process(es)");

                foreach (var process in processes)
                {
                    try
                    {
                        await LogMessage($"Closing Chrome process ID: {process.Id}");
                        process.CloseMainWindow();
                        
                        // Give process time to close gracefully
                        if (!process.WaitForExit(5000))
                        {
                            await LogMessage($"Process {process.Id} did not close gracefully, forcing termination");
                            process.Kill();
                        }
                        
                        await LogMessage($"Chrome process {process.Id} closed successfully");
                    }
                    catch (Exception ex)
                    {
                        await LogMessage($"Error closing Chrome process {process.Id}: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                await LogMessage($"Error closing Chrome processes: {ex.Message}");
            }
        }

        private static async Task ResetHostsFile()
        {
            try
            {
                await LogMessage("Resetting hosts file to blocked mode...");
                
                if (!File.Exists(_blockedHostsTemplatePath))
                {
                    await LogMessage($"ERROR: Blocked hosts template not found: {_blockedHostsTemplatePath}");
                    return;
                }

                // Read the blocked hosts template
                string hostsContent = await File.ReadAllTextAsync(_blockedHostsTemplatePath);
                
                // Write to system hosts file
                await File.WriteAllTextAsync(_systemHostsPath, hostsContent);
                
                await LogMessage("Hosts file reset to blocked mode successfully");
            }
            catch (Exception ex)
            {
                await LogMessage($"Error resetting hosts file: {ex.Message}");
            }
        }
    }

    public class SessionInfo
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Mode { get; set; } = "";
        public int DurationMinutes { get; set; }
        public string Reason { get; set; } = "";
    }
}
