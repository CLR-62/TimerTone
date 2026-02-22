using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using Avalonia.Threading;

namespace TimerTone.Core;

public class ProcessesMonitor
{
    private Timer _timer;
    private Dictionary<int, ProcessInfo> _processes = new();
    private readonly object _lock = new();
    
    public event EventHandler<ProcessEventArgs> ProcessStarted;
    public event EventHandler<ProcessEventArgs> ProcessStopped;
    
    public ProcessesMonitor()
    {
        _timer = new Timer(1000); // checking processes every sec(1k milsec)
        _timer.Elapsed += CheckProcesses;
    }
    
    public void Start()
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                _processes[process.Id] = new ProcessInfo
                {
                    Id = process.Id,
                    Name = process.ProcessName,
                    StartTime = process.StartTime
                };
            }
            catch { }
        }
        
        _timer.Start();
    }
    
    private void CheckProcesses(object sender, ElapsedEventArgs e)
    {
        try
        {
            var currentProcesses = Process.GetProcesses();
            var currentIds = new HashSet<int>(currentProcesses.Select(p => p.Id));
            
            foreach (var process in currentProcesses)
            {
                try
                {
                    lock (_lock)
                    {
                        if (!_processes.ContainsKey(process.Id))
                        {
                            var processInfo = new ProcessInfo
                            {
                                Id = process.Id,
                                Name = process.ProcessName,
                                StartTime = process.StartTime,
                                Path = GetProcessPath(process)
                            };
                            
                            _processes[process.Id] = processInfo;
                            
                            Dispatcher.UIThread.Post(() =>
                            {
                                ProcessStarted?.Invoke(this, new ProcessEventArgs
                                {
                                    ProcessName = processInfo.Name,
                                    ProcessId = processInfo.Id,
                                    Path = processInfo.Path,
                                    TimeStamp = DateTime.Now
                                });
                            });
                        }
                    }
                }
                catch { }
            }
            
            var stoppedIds = _processes.Keys.Where(id => !currentIds.Contains(id)).ToList();
            
            foreach (var id in stoppedIds)
            {
                lock (_lock)
                {
                    if (_processes.TryGetValue(id, out var processInfo))
                    {
                        _processes.Remove(id);
                        
                        Dispatcher.UIThread.Post(() =>
                        {
                            ProcessStopped?.Invoke(this, new ProcessEventArgs
                            {
                                ProcessName = processInfo.Name,
                                ProcessId = id,
                                TimeStamp = DateTime.Now
                            });
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking processes: {ex}");
        }
    }
    
    private string GetProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return "Access Denied";
        }
    }
    
    public void Stop()
    {
        _timer?.Stop();
    }
    
    public void Dispose()
    {
        _timer?.Dispose();
    }
    
    private class ProcessInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime StartTime { get; set; }
        public string Path { get; set; }
    }
}

public class ProcessEventArgs : EventArgs
{
    public string ProcessName { get; set; }
    public int ProcessId { get; set; }
    public string Path { get; set; }
    public string WindowTitle { get; set; }
    public DateTime TimeStamp { get; set; }
}