using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using TimerTone.Core;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Win32;

namespace TimerTone.UI;

public partial class MainWindow : Window
{
    public List<ProgramListItem> programsList { get; private set; } = new();
    public ExeIconsService iconsService;
    public ProcessesMonitor Monitor = new();
    public StartupHandler startupHandler = new();
    private bool monitoring = false;
    private bool isInAutoRun = false;
    private bool  needToStartMinimized = false;
    RegistryKey? startupKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
    
    private string _savesFolderPath = Path.Combine(AppContext.BaseDirectory, "Saves");

    public MainWindow(bool needToStartMinimized)
    {
        this.needToStartMinimized = needToStartMinimized;
        iconsService = new ExeIconsService();
        
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        HeadBorder.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        };
        
        
        if(File.Exists(Path.Combine(_savesFolderPath, "cfg.json")))
            LoadPrograms();
        
        
        Monitor.ProcessStarted += OnProcessOpened;
        Monitor.ProcessStopped += OnProcessClosed;
        
        ProgramStatusHandler.OnProgramStatusChanged += RefreshStatusLabel;
        
        NewProgramButton.Click += (s, e) =>
        {
            SelectProgram();
        };

        StartMonitor.Click += (s, e) =>
        {
            ToggleMonitor();
        };
        
        
        
        
        #region Autorun
        if (startupKey.GetValue("TimerTone", "NON") as string 
            == Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TimerTone.exe /minimized"))
        {
            isInAutoRun = true;
            AddToAutorun.IsVisible = false;
            RemoveFromAutorun.IsVisible = true;
        }

        AddToAutorun.Click += (s, e) =>
        {
            startupHandler.AddToStartup();
            isInAutoRun = true;
            AddToAutorun.IsVisible = false;
            RemoveFromAutorun.IsVisible = true;
        };

        RemoveFromAutorun.Click += (s, e) =>
        {
            startupHandler.RemoveFromStartup();
            isInAutoRun = false;
            AddToAutorun.IsVisible = true;
            RemoveFromAutorun.IsVisible = false;
        };
        #endregion

        if (!monitoring)
        {
            ProgramStatusHandler.ChangeProgramStatus(ProgramStatus.Pending);
        }
    }

    private void RefreshStatusLabel(object sender, ProgramStatusChangedEventArgs e)
    {
        StatusLabelBlock.Text = "Status: " + e.ProgramStatus;
        if (e.PrevProgramStatus != ProgramStatus.Monitoring)
        {
            var icons = new TrayIcons
            {
                new TrayIcon
                {
                    Icon = new WindowIcon(new Bitmap(AssetLoader.Open(new Uri("avares://TimerTone/Gfx/TrayIcons/appDisabled.ico")))),
                    Menu = [
                        new NativeMenuItem("Open"),
                        new NativeMenuItem("Start Monitoring"),
                        new NativeMenuItemSeparator(),
                        new NativeMenuItem("Exit")
                    ]
                }
            };
            
            //Fuck nullability possible. ^ there is guaranty
            
            NativeMenuItem openButton = icons[0].Menu.Items[0] as NativeMenuItem;
            openButton.Click += ((s, e) => { this.Show(); this.WindowState = WindowState.Normal; });
            
            NativeMenuItem exitButton = icons[0].Menu.Items[3] as NativeMenuItem;
            exitButton.Click += ((s, e) => { OnClose_Click(s, new RoutedEventArgs()); });
            
            NativeMenuItem startButton = icons[0].Menu.Items[1] as NativeMenuItem;
            startButton.Click += ((s, e) => { ToggleMonitor(); });

            TrayIcon.SetIcons(Application.Current, icons);
        }
        else if (e.ProgramStatus == ProgramStatus.Monitoring)
        {
            var icons = new TrayIcons
            {
                new TrayIcon
                {
                    Icon = new WindowIcon(new Bitmap(AssetLoader.Open(new Uri("avares://TimerTone/Gfx/TrayIcons/appEnabled.ico")))),
                    Menu = [
                        new NativeMenuItem("Open"),
                        new NativeMenuItem("Stop Monitoring"),
                        new NativeMenuItemSeparator(),
                        new NativeMenuItem("Exit")
                    ]
                }
            };
            
            //Fuck nullability possible. ^ there is guaranty
            
            NativeMenuItem openButton = icons[0].Menu.Items[0] as NativeMenuItem;
            openButton.Click += ((s, e) => { this.Show(); this.WindowState = WindowState.Normal; });
            
            NativeMenuItem exitButton = icons[0].Menu.Items[3] as NativeMenuItem;
            exitButton.Click += ((s, e) => { OnClose_Click(s, new RoutedEventArgs()); });
            
            NativeMenuItem stopButton = icons[0].Menu.Items[1] as NativeMenuItem;
            stopButton.Click += ((s, e) => { ToggleMonitor(); });

            TrayIcon.SetIcons(Application.Current, icons);
        }
        
        Console.WriteLine("Changed to " + e.ProgramStatus);



    }

    //Stupid thing for "trimming" and jsonSerializer both word(trimming in csproj)
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
    public void SavePrograms()
    {
        // Capture current timer state into ElapsedMs before saving
        foreach (var prog in programsList)
        {
            prog.ElapsedMs += prog.timer.ElapsedMilliseconds;
            prog.timer.Reset();
        }
        
        string json = JsonSerializer.Serialize(programsList);
        File.WriteAllText(Path.Combine(_savesFolderPath, "cfg.json"), json);
    }

    // the same
    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
    public async void LoadPrograms()
    {
        string json = File.ReadAllText(Path.Combine(_savesFolderPath, "cfg.json"));
        List<ProgramListItem> tempProgList = JsonSerializer.Deserialize<List<ProgramListItem>>(json) ?? new List<ProgramListItem>();
        foreach (var prog in tempProgList)
        {
            prog.progIcon = await iconsService.GetExeIcon(prog.Path);
        }
        programsList = new List<ProgramListItem>(tempProgList);
        RefreshProgramList(false);
        
        if (programsList.Count > 0)
        {
            monitoring = false;
            ToggleMonitor();
            if (needToStartMinimized)
            {
                Minimize();
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Monitor.ProcessStarted -= OnProcessOpened;
        Monitor.ProcessStopped -= OnProcessClosed;
        ProgramStatusHandler.OnProgramStatusChanged -= RefreshStatusLabel;
        Monitor.Dispose();
        foreach (var prog in programsList)
        {
            prog.timer.Stop();
        }

        SavePrograms();
        base.OnClosed(e);
    }

    private void OnProcessClosed(object? sender, ProcessEventArgs e)
    {
        if (programsList.Any(p => p.Name == e.ProcessName))
        {
            ProgramListItem prog = programsList.First(p => p.Name == e.ProcessName);
            prog.timer.Stop();
            prog.ElapsedMs += prog.timer.ElapsedMilliseconds;
            prog.timer.Reset();
            Console.WriteLine(prog.TotalElapsed);
            prog.activityDotColor = "#da3633";
            RefreshProgramList(true);
        }
    }

    private void OnProcessOpened(object? sender, ProcessEventArgs e)
    {
        if (programsList.Any(p => p.Name == e.ProcessName))
        {
            ProgramListItem prog = programsList.First(p => p.Name == e.ProcessName);
            prog.activityDotColor = "#3fb950";
            RefreshProgramList(false);
            prog.timer.Start();
        }
    }

    void RefreshProgramList(bool needSave)
    {
        programsListBox.Items.Clear();
        foreach (var program in programsList)
        {
            programsListBox.Items.Add(program);
        }
        
        if(needSave)
            SavePrograms();
    }

    public void ToggleMonitor()
    {
        StartMonitor.Background = new SolidColorBrush(Colors.Transparent);
        StartMonitor.Classes.Clear();
        monitoring = !monitoring;
        if (!monitoring)
        {
            StartMonitorLabel.Text = "Start";
            StartMonitor.Classes.Add("Secondary");
            Monitor.Stop();
            ProgramStatusHandler.ChangeProgramStatus(ProgramStatus.Pending);
        }
        else
        {
            StartMonitorLabel.Text = "Stop";
            StartMonitor.Classes.Add("Danger");
            Monitor.Start();
            ProgramStatusHandler.ChangeProgramStatus(ProgramStatus.Monitoring);
        }
    }

    public async void SelectProgram()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Exe file",
            AllowMultiple = false
        });

        if (files.Count >= 1)
        {
            string path = files[0].Path.AbsolutePath.Replace("/", @"\").Replace("%20", " ");
            await AddProgramByPath(path);
        }
        else
        {
            if(ProgramStatusHandler.CurrentProgramStatus != ProgramStatus.Monitoring)
                ProgramStatusHandler.ChangeProgramStatus(ProgramStatus.ProgramAddSequenceAborted);
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var files = e.Data.GetFiles();
        if (files == null) return; 
        foreach (var file in files)
        {
            string path = file.Path.AbsolutePath.Replace("/", @"\").Replace("%20", " ");
            await AddProgramByPath(path);
            
        }
    }

    private async Task AddProgramByPath(string path)
    {
        if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            ProgramStatusHandler.ChangeProgramStatus(ProgramStatus.ErrorPAx0);
            return;
        }

        try
        {
            ProgramListItem item = new ProgramListItem
            {
                Name = path.Split(@"\").Last().Split('.').First(),
                Path = path,
                progIcon = await iconsService.GetExeIcon(path),
                timer = new Stopwatch(),
                activityDotColor = "#da3633"
            };

            if (!programsList.Any(p => p.Path == item.Path))
            {
                programsList.Add(item);
                RefreshProgramList(true);
                ProgramStatusHandler.ChangeProgramStatus(ProgramStatus.ProgramAddSequenceCompleted);
            }
            else
            {
                if(!monitoring)
                    ProgramStatusHandler.ChangeProgramStatus(ProgramStatus.ErrorPAx2);
            }
        }
        catch
        {
            if(!monitoring)
                ProgramStatusHandler.ChangeProgramStatus(ProgramStatus.ErrorPAx1);
        }
    }

    private void DeleteProgramFromList(object? sender, RoutedEventArgs e)
    {
        Button button = sender as Button;

        ProgramListItem item = button.CommandParameter as ProgramListItem;
        
        if (item != null && programsList.Contains(item))
        {
            programsList.Remove(item);
            RefreshProgramList(true);
        }
    }

    private void OnMinimize_Click(object? sender, RoutedEventArgs e)
    {
        Minimize();
    }

    public void Minimize()
    {
        this.WindowState = WindowState.Minimized;
        this.Hide();
    }

    private void OnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}