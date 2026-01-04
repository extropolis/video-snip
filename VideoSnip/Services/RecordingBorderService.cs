using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using VideoSnip.Helpers;
using VideoSnip.Models;

namespace VideoSnip.Services;

public class RecordingBorderService : IDisposable
{
    private Window? _borderWindow;
    private bool _disposed;
    private const int BorderThickness = 3;
    private DispatcherTimer? _topmostTimer;

    public void ShowBorder(RecordingRegion region)
    {
        // Create 4 separate border windows (top, bottom, left, right) to avoid off-screen issues
        _borderWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            IsHitTestVisible = false,
            Left = region.X,
            Top = region.Y,
            Width = region.Width,
            Height = region.Height,
            ResizeMode = ResizeMode.NoResize
        };

        // Use a Grid with 4 rectangles for the border (inside the region)
        var grid = new Grid();

        // Top border
        var topBorder = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromRgb(255, 68, 68)),
            Height = BorderThickness,
            VerticalAlignment = VerticalAlignment.Top
        };
        grid.Children.Add(topBorder);

        // Bottom border
        var bottomBorder = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromRgb(255, 68, 68)),
            Height = BorderThickness,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        grid.Children.Add(bottomBorder);

        // Left border
        var leftBorder = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromRgb(255, 68, 68)),
            Width = BorderThickness,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        grid.Children.Add(leftBorder);

        // Right border
        var rightBorder = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromRgb(255, 68, 68)),
            Width = BorderThickness,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        grid.Children.Add(rightBorder);

        _borderWindow.Content = grid;
        _borderWindow.Show();

        // Exclude the border window from screen capture so it's visible on screen but not in recordings
        var hwnd = new WindowInteropHelper(_borderWindow).Handle;
        NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);

        // Timer to keep the border window on top (WPF Topmost can lose effect)
        _topmostTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _topmostTimer.Tick += (s, e) =>
        {
            if (_borderWindow != null && _borderWindow.IsVisible)
            {
                _borderWindow.Topmost = false;
                _borderWindow.Topmost = true;
            }
        };
        _topmostTimer.Start();
    }

    public void HideBorder()
    {
        _topmostTimer?.Stop();
        _topmostTimer = null;
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
