using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public class IdleTrackingService : BackgroundService
{
    private readonly SmtpOptions _smtp;
    private readonly IdleOptions _idle;
    private readonly string _machine = Environment.MachineName;

    private bool _isCurrentlyIdle = false;
    private bool _alertSentForThisIdle = false;
    private DateTime? _idleBeganAt = null;

    public IdleTrackingService(IOptions<SmtpOptions> smtp, IOptions<IdleOptions> idle)
    {
        _smtp = smtp.Value;
        _idle = idle.Value;

        // Handle crashes/kills
        AppDomain.CurrentDomain.ProcessExit += (_, __) =>
            TryNotify("Service process exit", $"The service process is exiting on {_machine} at {DateTime.Now}.");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            TryNotify("Service crashed", $"Unhandled exception on {_machine}: {e.ExceptionObject}");
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    private int GetIdleTimeSeconds()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
        if (!GetLastInputInfo(ref info)) return 0;
        uint idleTicks = (uint)Environment.TickCount - info.dwTime;
        return (int)(idleTicks / 1000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        EnsureLogDirectory();

        Log("Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            int idleSeconds = GetIdleTimeSeconds();

            if (idleSeconds >= _idle.ThresholdSeconds)
            {
                if (!_isCurrentlyIdle)
                {
                    _isCurrentlyIdle = true;
                    _alertSentForThisIdle = false;
                    _idleBeganAt = DateTime.Now.AddSeconds(-idleSeconds);
                    Log($"Idle detected (threshold reached: {_idle.ThresholdSeconds / 60} minute(s)).");
                }

                if (!_alertSentForThisIdle)
                {
                    // Always report threshold duration in minutes, not the raw current idle time
                    var thresholdMinutes = Math.Round(_idle.ThresholdSeconds / 60.0, 2);
                    var subject = "User idle detected";
                    var body =
                        $"Machine: {_machine}\n" +
                        $"Idle Duration (min): {thresholdMinutes}\n" +
                        $"Idle Since: {_idleBeganAt:yyyy-MM-dd HH:mm:ss}\n" +
                        $"Detected At: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    if (TryNotify(subject, body))
                    {
                        Log($"Email sent to {_smtp.To} after reaching {_idle.ThresholdSeconds} seconds of idle.");
                        _alertSentForThisIdle = true;
                    }
                }
            }
            else
            {
                if (_isCurrentlyIdle)
                {
                    _isCurrentlyIdle = false;
                    _alertSentForThisIdle = false;
                    Log("User active.");
                    _idleBeganAt = null;
                }
            }

            await Task.Delay(_idle.CheckIntervalMs, stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        Log("Service stopping.");
        TryNotify("Service stopped", $"The idle tracking service was stopped on {_machine} at {DateTime.Now}.");
        return base.StopAsync(cancellationToken);
    }

    private bool TryNotify(string subject, string body)
    {
        try
        {
            using var client = new SmtpClient(_smtp.Host, _smtp.Port)
            {
                EnableSsl = _smtp.EnableSsl,
                Credentials = new NetworkCredential(_smtp.Username, _smtp.Password)
            };
            var msg = new MailMessage(_smtp.From, _smtp.To, subject, body);
            client.Send(msg);
            return true;
        }
        catch (Exception ex)
        {
            Log($"Failed to send email: {ex.Message}");
            return false;
        }
    }

    private void EnsureLogDirectory()
    {
        try
        {
            var dir = Path.GetDirectoryName(_idle.LogPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir!);
        }
        catch { }
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        try
        {
            EnsureLogDirectory();
            File.AppendAllText(_idle.LogPath, line + Environment.NewLine);
        }
        catch
        {
            Console.WriteLine(line); // fallback
        }
    }
}
