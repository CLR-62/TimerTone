using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace TimerTone.Core;

public class StartupHandler
{
    public void AddToStartup()
    {
        RegistryKey? startupKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
        startupKey.SetValue("TimerTone", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TimerTone.exe"));
        startupKey.Close();
        startupKey.Dispose();
    }

    public void RemoveFromStartup()
    {
        RegistryKey? startupKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
        startupKey.DeleteValue("TimerTone");
        startupKey.Close();
        startupKey.Dispose();
    }
}