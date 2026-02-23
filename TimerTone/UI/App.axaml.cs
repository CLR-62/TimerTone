using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace TimerTone.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
         if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "Saves")))
         {
             Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Saves"));
         }

         try
         {
             if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
             {
                 desktop.MainWindow = new MainWindow(desktop.Args.Contains("/minimized"));
             }

             base.OnFrameworkInitializationCompleted();
         }
         catch (Exception e)
         {
             MessageBox msg = new MessageBox(e.Message);
             msg.Show();
         }
    }
}