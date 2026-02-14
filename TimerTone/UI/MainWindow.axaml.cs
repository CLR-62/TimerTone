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
namespace TimerTone.UI;

public partial class MainWindow : Window
{
    public List<ProgramListItem> programsList { get; private set; } = new();
    public ExeIconsService iconsService;
    public ProcessesMonitor Monitor = new();
    private bool monitoring = false;
    
    private string SavesFolderPath = Path.Combine(AppContext.BaseDirectory, "Saves");

    public MainWindow()
    {
        iconsService = new ExeIconsService();
        
        InitializeComponent();
        if(File.Exists(Path.Combine(SavesFolderPath, "cfg.json")))
            LoadPrograms();
        AddHandler(DragDrop.DropEvent, OnDrop);
        Monitor.ProcessStarted += OnProcessOpened;
        Monitor.ProcessStopped += OnProcessClosed;
        ProgramStatusHandler.OnProgramStatusChanged += RefreshStatusLabel;
        HeadBorder.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        };

        NewProgramButton.Click += (s, e) =>
        {
            SelectProgram();
        };

        StartMonitor.Click += (s, e) =>
        {
            ToggleMonitor();
        };
        ProgramStatusHandler.ChangeProgramStatus(ProgramStatus.Pending);
        
        if (programsList.Count > 0)
        {
            monitoring = false;
            ToggleMonitor();
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
            NativeMenuItem openButton = icons[0].Menu.Items[0] as NativeMenuItem;
            openButton.Click += ((s, e) => { this.Show(); this.WindowState = WindowState.Normal; });
            
            NativeMenuItem exitButton = icons[0].Menu.Items[3] as NativeMenuItem;
            exitButton.Click += ((s, e) => { OnClose_Click(s, new RoutedEventArgs()); });
            
            NativeMenuItem stopButton = icons[0].Menu.Items[1] as NativeMenuItem;
            stopButton.Click += ((s, e) => { ToggleMonitor(); });

            TrayIcon.SetIcons(Application.Current, icons);
        }
    }
    
    

    public void SavePrograms()
    {
        // Capture current timer state into ElapsedMs before saving
        foreach (var prog in programsList)
        {
            prog.ElapsedMs += prog.timer.ElapsedMilliseconds;
            prog.timer.Reset();
        }
        
        string json = JsonSerializer.Serialize(programsList);
        File.WriteAllText(Path.Combine(SavesFolderPath, "cfg.json"), json);
    }

    public async void LoadPrograms()
    {
        string json = File.ReadAllText(Path.Combine(SavesFolderPath, "cfg.json"));
        List<ProgramListItem> tempProgList = JsonSerializer.Deserialize<List<ProgramListItem>>(json) ?? new List<ProgramListItem>();
        foreach (var prog in tempProgList)
        {
            prog.progIcon = await iconsService.GetExeIcon(prog.Path);
        }
        programsList = new List<ProgramListItem>(tempProgList);
        RefreshProgramList();
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
            // Accumulate elapsed time from this session
            prog.ElapsedMs += prog.timer.ElapsedMilliseconds;
            prog.timer.Reset();
            Console.WriteLine(prog.TotalElapsed);
            RefreshProgramList();
        }
    }

    private void OnProcessOpened(object? sender, ProcessEventArgs e)
    {
        if (programsList.Any(p => p.Name == e.ProcessName))
        {
            ProgramListItem prog = programsList.First(p => p.Name == e.ProcessName);
            prog.timer.Start();
        }
    }

    void RefreshProgramList()
    {
        programsListBox.Items.Clear();
        foreach (var program in programsList)
        {
            programsListBox.Items.Add(program);
        }
        
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
                timer = new Stopwatch()
            };

            if (!programsList.Any(p => p.Path == item.Path))
            {
                programsList.Add(item);
                RefreshProgramList();
                ProgramStatusHandler.ChangeProgramStatus(ProgramStatus.ProgramAddSequenceCompleted);
            }
            else
            {
                if(ProgramStatusHandler.CurrentProgramStatus != ProgramStatus.Monitoring)
                    ProgramStatusHandler.ChangeProgramStatus(ProgramStatus.ErrorPAx2);
            }
        }
        catch
        {
            if(ProgramStatusHandler.CurrentProgramStatus != ProgramStatus.Monitoring)
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
            RefreshProgramList();
        }
    }

    private void OnMinimize_Click(object? sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
        this.Hide();
    }

    private void OnClose_Click(object? sender, RoutedEventArgs e)
    {
        OnClosed(new EventArgs());
        Environment.Exit(0);
    }
}