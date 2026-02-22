using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TimerTone.UI;

public partial class MessageBox : Window
{
    private string _message;
    
    public override void Show()
    {
        datatext.Text = _message;
        base.Show();
    }


    public MessageBox(string text)
    {
        _message = text;
        InitializeComponent();
        
        HeadBorder.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        };
    }

    private void OnMinimize_Click(object? sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void OnClose_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}