﻿using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Security.Principal;

namespace ChromeStarter
{
  class Program
  {
    private static IConfiguration? _configuration;
    private static string _sessionLogPath = "";
    private static string _systemHostsPath = "";
    private static string _cleanHostsTemplatePath = "";

    static async Task Main(string[] args)
    {
      Console.WriteLine("ChromeStarter - Chrome Session Manager");
      Console.WriteLine("=====================================");

      // Load configuration
      LoadConfiguration();

      // Check for active sessions first
      await CheckAndDisplayActiveSession();

      // Check if running as administrator
      if (!IsRunAsAdministrator())
      {
        Console.WriteLine("WARNING: Not running as administrator!");
        Console.WriteLine("This application requires administrator privileges to modify the hosts file.");
        Console.WriteLine("Some features may not work properly.");
        Console.WriteLine();
      }

      try
      {
        // Get user input for session configuration
        var sessionConfig = GetSessionConfiguration();

        // Update hosts file based on user choice
        if (sessionConfig.AllowAllPages) await UpdateHostsFile();

        // Log the session
        await LogSession(sessionConfig);

        // Launch Chrome (only if not already running)
        LaunchChrome();

        Console.WriteLine();
        if (IsChromeRunning())
        {
          Console.WriteLine("Chrome session configured successfully!");
        }
        else
        {
          Console.WriteLine("Chrome session started successfully!");
        }
        Console.WriteLine($"Session will expire at: {sessionConfig.EndTime:HH:mm:ss}");
        // wait 10 seconds before exiting
        await Task.Delay(10000);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error: {ex.Message}");
        await Task.Delay(10000);
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
      _cleanHostsTemplatePath = _configuration["AppSettings:CleanHostsTemplatePath"] ?? "";

      // Ensure the directory for the session log exists
      var logDirectory = Path.GetDirectoryName(_sessionLogPath);
      if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
      {
        Directory.CreateDirectory(logDirectory);
      }
    }

    private static bool IsRunAsAdministrator()
    {
      var identity = WindowsIdentity.GetCurrent();
      var principal = new WindowsPrincipal(identity);
      return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static async Task CheckAndDisplayActiveSession()
    {
      try
      {
        if (!File.Exists(_sessionLogPath))
        {
          Console.WriteLine("No session history found.");
          Console.WriteLine();
          return;
        }

        var lines = await File.ReadAllLinesAsync(_sessionLogPath);
        if (lines.Length == 0)
        {
          Console.WriteLine("No session history found.");
          Console.WriteLine();
          return;
        }

        // Get the most recent session (last line)
        var lastLine = lines[^1];
        var session = ParseSessionLine(lastLine);
        
        if (session == null)
        {
          Console.WriteLine("Unable to parse most recent session.");
          Console.WriteLine();
          return;
        }

        // Check if session is still active
        if (DateTime.Now <= session.EndTime)
        {
          var remainingTime = session.EndTime - DateTime.Now;
          var remainingMinutes = (int)Math.Ceiling(remainingTime.TotalMinutes);
          
          Console.WriteLine($"ACTIVE SESSION FOUND:");
          Console.WriteLine($"  Mode: {session.Mode}");
          Console.WriteLine($"  Started: {session.StartTime:HH:mm:ss}");
          Console.WriteLine($"  Expires: {session.EndTime:HH:mm:ss}");
          Console.WriteLine($"  Remaining: {remainingMinutes} minute(s)");
          Console.WriteLine($"  Reason: {session.Reason}");
          Console.WriteLine();
        }
        else
        {
          var expiredMinutes = (int)Math.Floor((DateTime.Now - session.EndTime).TotalMinutes);
          Console.WriteLine($"Last session expired {expiredMinutes} minute(s) ago at {session.EndTime:HH:mm:ss}");
          Console.WriteLine();
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error checking for active session: {ex.Message}");
        Console.WriteLine();
      }
    }

    private static SessionConfiguration? ParseSessionLine(string line)
    {
      try
      {
        // Expected format: YYYY-MM-DD HH:MM:SS | ALL/LIMITED | DURATION_MINUTES | END_TIME_HH:MM:SS | REASON
        var parts = line.Split('|').Select(p => p.Trim()).ToArray();
        
        if (parts.Length != 5)
        {
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

        return new SessionConfiguration
        {
          StartTime = startTime,
          EndTime = endTime,
          Mode = mode,
          DurationMinutes = durationMinutes,
          Reason = reason,
          AllowAllPages = mode == "ALL"
        };
      }
      catch
      {
        return null;
      }
    }

    private static SessionConfiguration GetSessionConfiguration()
    {
      var config = new SessionConfiguration();

      // Only ask about host file configuration if running as administrator
      if (IsRunAsAdministrator())
      {
        // Get host file configuration
        Console.WriteLine("Unblock Distracting Pages?");
        Console.WriteLine("Y - Unlock All pages (no blocks)");
        Console.WriteLine("N - Limited pages (with blocks)");
        Console.Write("Choose mode (Y/N): ");

        var modeInput = Console.ReadLine()?.ToUpper();
        while (modeInput != "Y" && modeInput != "N")
        {
          Console.Write("Invalid input. Please enter Y or N: ");
          modeInput = Console.ReadLine()?.ToUpper();
        }

        config.AllowAllPages = modeInput == "Y";
        config.Mode = config.AllowAllPages ? "ALL" : "LIMITED";
      }
      else
      {
        // Not running as admin - default to LIMITED mode (no hosts file changes)
        config.AllowAllPages = false;
        config.Mode = "LIMITED";
        Console.WriteLine("Running in LIMITED mode (hosts file will not be modified - requires admin privileges)");
      }

      // Get session duration
      Console.Write("How many minutes do you need Chrome? (max 15): ");
      var durationInput = Console.ReadLine();
      int duration;
      while (!int.TryParse(durationInput, out duration) || duration <= 0)
      {
        Console.Write("Invalid input. Please enter a positive number: ");
        durationInput = Console.ReadLine();
      }
      
      // Cap duration at 15 minutes
      if (duration > 15)
      {
        Console.WriteLine($"Duration capped at 15 minutes (requested: {duration} minutes)");
        duration = 15;
      }
      
      config.DurationMinutes = duration;

      // Get reason for use
      Console.Write("Please provide a brief reason for using Chrome: ");
      config.Reason = Console.ReadLine() ?? "";
      while (string.IsNullOrWhiteSpace(config.Reason))
      {
        Console.Write("Reason cannot be empty. Please provide a reason: ");
        config.Reason = Console.ReadLine() ?? "";
      }

      // Calculate session times
      config.StartTime = DateTime.Now;
      config.EndTime = config.StartTime.AddMinutes(config.DurationMinutes);

      return config;
    }

    private static async Task UpdateHostsFile()
    {

      if (!IsRunAsAdministrator())
      {
        Console.WriteLine("Cannot update hosts file - administrator privileges required.");
        return;
      }

      try
      {
        string sourceFile = _cleanHostsTemplatePath;

        if (!File.Exists(sourceFile))
        {
          throw new FileNotFoundException($"Template hosts file not found: {sourceFile}");
        }

        // Read the template file
        string hostsContent = await File.ReadAllTextAsync(sourceFile);

        // Write to system hosts file
        await File.WriteAllTextAsync(_systemHostsPath, hostsContent);

        Console.WriteLine($"Hosts file updated to unrestricted mode.");
      }
      catch (Exception ex)
      {
        throw new Exception($"Failed to update hosts file: {ex.Message}");
      }
    }

    private static async Task LogSession(SessionConfiguration config)
    {
      try
      {
        var logEntry = $"{config.StartTime:yyyy-MM-dd HH:mm:ss} | {config.Mode} | {config.DurationMinutes} | {config.EndTime:HH:mm:ss} | {config.Reason}";

        await File.AppendAllTextAsync(_sessionLogPath, logEntry + Environment.NewLine);

        Console.WriteLine("Session logged successfully.");
      }
      catch (Exception ex)
      {
        throw new Exception($"Failed to log session: {ex.Message}");
      }
    }

    private static bool IsChromeRunning()
    {
      try
      {
        var chromeProcesses = Process.GetProcessesByName("chrome");
        return chromeProcesses.Length > 0;
      }
      catch
      {
        return false;
      }
    }

    private static void LaunchChrome()
    {
      try
      {
        // Check if Chrome is already running
        if (IsChromeRunning())
        {
          Console.WriteLine("Chrome is already running - session extended without launching new instance.");
          return;
        }

        // Try common Chrome installation paths
        string[] chromePaths = {
                    @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                    @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                    @"C:\Users\" + Environment.UserName + @"\AppData\Local\Google\Chrome\Application\chrome.exe"
                };

        string? chromePath = chromePaths.FirstOrDefault(File.Exists);

        if (chromePath == null)
        {
          throw new FileNotFoundException("Chrome executable not found in common locations.");
        }

        Process.Start(new ProcessStartInfo
        {
          FileName = chromePath,
          UseShellExecute = true
        });

        Console.WriteLine("Chrome launched successfully.");
      }
      catch (Exception ex)
      {
        throw new Exception($"Failed to launch Chrome: {ex.Message}");
      }
    }
  }

  public class SessionConfiguration
  {
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Mode { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string Reason { get; set; } = "";
    public bool AllowAllPages { get; set; }
  }
}
