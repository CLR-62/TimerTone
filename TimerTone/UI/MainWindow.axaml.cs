using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
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
    
    public ProgramStatus currentProgramStatus = ProgramStatus.Loading;

    public MainWindow()
    {
        iconsService = new ExeIconsService();
        
        InitializeComponent();
        if(File.Exists(Path.Combine(SavesFolderPath, "cfg.json")))
            LoadPrograms();
        AddHandler(DragDrop.DropEvent, OnDrop);
        Monitor.ProcessStarted += OnProcessOpened;
        Monitor.ProcessStopped += OnProcessClosed;
        ChangeProgramStatus(ProgramStatus.Loading);
        HeadBorder.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        };
        ChangeProgramStatus(ProgramStatus.Pending);

        NewProgramButton.Click += (s, e) =>
        {
            SelectProgram();
        };

        StartMonitor.Click += (s, e) =>
        {
            ToggleMonitor();
        };
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
        if (programsList.Count > 0)
        {
            monitoring = false;
            ToggleMonitor();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Monitor.ProcessStarted -= OnProcessOpened;
        Monitor.ProcessStopped -= OnProcessClosed;
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
        }
        else
        {
            StartMonitorLabel.Text = "Stop";
            StartMonitor.Classes.Add("Danger");
            Monitor.Start();
        }
    }

    public void ChangeProgramStatus(ProgramStatus status)
    {
        currentProgramStatus = status;
        switch (currentProgramStatus)
        {
            case ProgramStatus.Loading:
                StatusLabelBlock.Text = "Status: Loading...";
                break;
            case ProgramStatus.Pending:
                StatusLabelBlock.Text = "Status: Pending";
                break;
            case ProgramStatus.NewProgOpened:
                StatusLabelBlock.Text = "Status: Program Opened";
                break;
            case ProgramStatus.ProgramAddSequenceAborted:
                StatusLabelBlock.Text = "Status: Add Sequence Aborted";
                break;
            case ProgramStatus.ProgramAddSequenceCompleted:
                StatusLabelBlock.Text = "Status: Add Sequence Completed";
                break;
            case ProgramStatus.ErrorPAx0:
                StatusLabelBlock.Text = "Status: Error PAx0";
                break;
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
            ChangeProgramStatus(ProgramStatus.ProgramAddSequenceAborted);
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
            ChangeProgramStatus(ProgramStatus.ErrorPAx0);
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
                ChangeProgramStatus(ProgramStatus.ProgramAddSequenceCompleted);
            }
            else
            {
                ChangeProgramStatus(ProgramStatus.ErrorPAx2);
            }
        }
        catch
        {
            ChangeProgramStatus(ProgramStatus.ErrorPAx1);
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
    }

    private void OnClose_Click(object? sender, RoutedEventArgs e)
    {
        OnClosed(new EventArgs());
        Environment.Exit(0);
    }
}

public enum ProgramStatus
{
    Loading,
    Pending,
    NewProgOpened,
    ProgramAddSequenceAborted,
    ProgramAddSequenceCompleted,
    ErrorPAx0, //NON EXE
    ErrorPAx1, //ERROR DURING CREATING PROGRAM ITEM
    ErrorPAx2, //ALREADY ADDED
    Saving,
    Saved
}