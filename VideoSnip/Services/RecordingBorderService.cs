using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using VideoSnip.Models;

namespace VideoSnip.Services;

public class RecordingBorderService : IDisposable
{
    private Window? _borderWindow;
    private bool _disposed;
    private const int BorderThickness = 3;

    public void ShowBorder(RecordingRegion region)
    {
        // Create a transparent window that covers the recording region with a red border
        _borderWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            IsHitTestVisible = false,
            Left = region.X - BorderThickness,
            Top = region.Y - BorderThickness,
            Width = region.Width + (BorderThickness * 2),
            Height = region.Height + (BorderThickness * 2),
            ResizeMode = ResizeMode.NoResize
        };

        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(255, 68, 68)), // Red border
            BorderThickness = new Thickness(BorderThickness),
            Background = Brushes.Transparent
        };

        _borderWindow.Content = border;
        _borderWindow.Show();
    }

    public void HideBorder()
    {
        _borderWindow?.Close();
        _borderWindow = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        HideBorder();
        GC.SuppressFinalize(this);
    }
}
