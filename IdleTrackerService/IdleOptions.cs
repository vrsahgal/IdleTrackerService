public class IdleOptions
{
    public int ThresholdSeconds { get; set; } = 300;
    public int CheckIntervalMs { get; set; } = 5000;
    public string LogPath { get; set; } = @"C:\ProgramData\IdleTracker\idle-log.txt";
}
