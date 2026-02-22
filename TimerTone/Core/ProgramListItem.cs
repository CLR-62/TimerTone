using System;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TimerTone.Core;

public class ProgramListItem
{
    public string Name { get; set; }
    public string Path { get; set; }
    
    public long ElapsedMs { get; set; }
    
    [JsonIgnore]  
    public Bitmap? progIcon { get; set; }
    
    [JsonIgnore]
    public Stopwatch timer { get; set; } = new();
    
    [JsonIgnore]
    public string activityDotColor { get; set; } = "#da3633";
    
    [JsonIgnore]
    public TimeSpan TotalElapsed => TimeSpan.FromMilliseconds(ElapsedMs) + timer.Elapsed;
}