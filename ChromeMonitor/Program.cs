using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ChromeMonitor
{
    class Program
    {
        // P/Invoke declarations for Windows MessageBox
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);

        // MessageBox constants
        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONWARNING = 0x00000030;
        private const uint MB_TOPMOST = 0x00040000;
        private const uint MB_SYSTEMMODAL = 0x00001000;
        private static IConfiguration? _configuration;
        private static string _sessionLogPath = "";
        private static string _systemHostsPath = "";
        private static string _blockedHostsTemplatePath = "";
        private static int _warningTimeoutSeconds = 55;
        private static string _chromeProcessName = "chrome";

        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine($"ChromeMonitor - Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
                // Load configuration
                LoadConfiguration();

                // Check if running as administrator
                if (!IsRunAsAdministrator())
                {
                    Console.WriteLine("ERROR: Not running as administrator!");
                    Console.WriteLine("ChromeMonitor requires administrator privileges to modify hosts file and manage processes.");
                    Environment.Exit(1);
                }

                // Main monitoring logic
                await PerformMonitoringCycle();
                
                Console.WriteLine($"ChromeMonitor - Completed at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL ERROR: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }

        private static void LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            _configuration = builder.Build();

            _sessionLogPath = _configuration["AppSettings:SessionLogPath"] ?? "";
            _systemHostsPath = _configuration["AppSettings:SystemHostsPath"] ?? "";
            
            _blockedHostsTemplatePath = _configuration["AppSettings:BlockedHostsTemplatePath"] ?? "";
            _warningTimeoutSeconds = int.Parse(_configuration["AppSettings:WarningTimeoutSeconds"] ?? "55");
            _chromeProcessName = _configuration["AppSettings:ChromeProcessName"] ?? "chrome";

            Console.WriteLine("Configuration loaded successfully.");
        }

        private static bool IsRunAsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static async Task PerformMonitoringCycle()
        {
            Console.WriteLine("Starting monitoring cycle...");

            // Step 1: Check for active session
            var activeSession = CheckForActiveSession();
            Console.WriteLine($"Active session check: {(activeSession != null ? "Found valid session" : "No valid session")}");

            // Step 2: Check for Chrome processes
            var chromeProcesses = GetChromeProcesses();
            Console.WriteLine($"Chrome processes found: {chromeProcesses.Length}");

            // Step 3: Handle Chrome closure if needed
            if (chromeProcesses.Length > 0 && activeSession == null)
            {
                Console.WriteLine("Chrome is running without valid session - initiating warning and closure sequence");
                await HandleChromeWarningAndClosure(chromeProcesses);
            }
            else if (chromeProcesses.Length > 0 && activeSession != null)
            {
                Console.WriteLine($"Chrome is running with valid session (expires at {activeSession.EndTime:HH:mm:ss})");
            }
            else
            {
                Console.WriteLine("No Chrome processes found");
            }

            // Step 4: Reset hosts file (always done)
            await ResetHostsFile();
        }

        private static SessionInfo? CheckForActiveSession()
        {
            try
            {
                if (!File.Exists(_sessionLogPath))
                {
                    Console.WriteLine("Session log file does not exist");
                    return null;
                }

                var lines = File.ReadAllLines(_sessionLogPath);
                if (lines.Length == 0)
                {
                    Console.WriteLine("Session log file is empty");
                    return null;
                }

                // Get the most recent session (last line)
                var lastLine = lines[^1];
                var session = ParseSessionLine(lastLine);
                
                if (session == null)
                {
                    Console.WriteLine("Failed to parse most recent session");
                    return null;
                }

                // Check if session is still active
                if (DateTime.Now <= session.EndTime)
                {
                    Console.WriteLine($"Found active session: {session.Mode}, expires at {session.EndTime:HH:mm:ss}, reason: {session.Reason}");
                    return session;
                }
                else
                {
                    Console.WriteLine($"Most recent session expired at {session.EndTime:HH:mm:ss}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for active session: {ex.Message}");
                return null;
            }
        }

        private static SessionInfo? ParseSessionLine(string line)
        {
            try
            {
                // Expected format: YYYY-MM-DD HH:MM:SS | ALL/LIMITED | DURATION_MINUTES | END_TIME_HH:MM:SS | REASON
                var parts = line.Split('|').Select(p => p.Trim()).ToArray();
                
                if (parts.Length != 5)
                {
                    Console.WriteLine($"Invalid session line format: {line}");
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
                Console.WriteLine($"Error parsing session line '{line}': {ex.Message}");
                return null;
            }
        }

        private static Process[] GetChromeProcesses()
        {
            try
            {
                return Process.GetProcessesByName(_chromeProcessName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting Chrome processes: {ex.Message}");
                return Array.Empty<Process>();
            }
        }

        private static async Task HandleChromeWarningAndClosure(Process[] chromeProcesses)
        {
            try
            {
                // Display warning message box
                Console.WriteLine("Displaying warning message to user...");
                ShowWarningMessageBox();

                // Wait for the configured timeout period
                Console.WriteLine($"Waiting {_warningTimeoutSeconds} seconds for user response...");
                await Task.Delay(_warningTimeoutSeconds * 1000);

                // Re-check for active session after timeout
                Console.WriteLine("Re-checking for active session after warning period...");
                var activeSession = CheckForActiveSession();

                if (activeSession == null)
                {
                    Console.WriteLine("No valid session found after warning period - closing Chrome processes");
                    CloseChromeProcesses();
                }
                else
                {
                    Console.WriteLine("Valid session found after warning period - Chrome will continue running");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in warning and closure sequence: {ex.Message}");
            }
        }

        private static void ShowWarningMessageBox()
        {
            try
            {
                var message = "Chrome will shut down in 1 minute.\n\nRe-run ChromeStarter.exe to extend your session if needed.";
                var title = "ChromeGuard Warning";
                
                // Use native Windows MessageBox with TopMost and SystemModal flags
                // This ensures the message appears even when running as a background service
                uint flags = MB_OK | MB_ICONWARNING | MB_TOPMOST | MB_SYSTEMMODAL;
                
                Console.WriteLine($"Displaying warning message box: {title}");
                Console.WriteLine($"Message: {message}");
                
                // Show the message box - this will appear even if the console is hidden
                int result = MessageBox(IntPtr.Zero, message, title, flags);
                
                Console.WriteLine("Warning message box displayed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing warning message: {ex.Message}");
                // Fallback to console output
                Console.WriteLine("FALLBACK WARNING: Chrome will shut down in 1 minute!");
            }
        }

        private static void CloseChromeProcesses()
        {
            try
            {
                var processes = GetChromeProcesses();
                Console.WriteLine($"Attempting to close {processes.Length} Chrome process(es)");

                foreach (var process in processes)
                {
                    try
                    {
                        Console.WriteLine($"Closing Chrome process ID: {process.Id}");
                        process.CloseMainWindow();
                        
                        // Give process time to close gracefully
                        if (!process.WaitForExit(5000))
                        {
                            Console.WriteLine($"Process {process.Id} did not close gracefully, forcing termination");
                            process.Kill();
                        }
                        
                        Console.WriteLine($"Chrome process {process.Id} closed successfully");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error closing Chrome process {process.Id}: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing Chrome processes: {ex.Message}");
            }
        }

        private static async Task ResetHostsFile()
        {
            try
            {
                Console.WriteLine("Resetting hosts file to blocked mode...");
                
                if (!File.Exists(_blockedHostsTemplatePath))
                {
                    Console.WriteLine($"ERROR: Blocked hosts template not found: {_blockedHostsTemplatePath}");
                    return;
                }

                // Read the blocked hosts template
                string hostsContent = await File.ReadAllTextAsync(_blockedHostsTemplatePath);
                
                // Write to system hosts file
                await File.WriteAllTextAsync(_systemHostsPath, hostsContent);
                
                Console.WriteLine("Hosts file reset to blocked mode successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resetting hosts file: {ex.Message}");
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
