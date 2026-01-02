using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using VideoSnip.Helpers;
using VideoSnip.Models;

namespace VideoSnip.Views;

public partial class RegionSelector : Window
{
    private readonly VideoCaptureMode _mode;
    private readonly AspectRatioPreset? _aspectRatio;
    private Point _startPoint;
    private bool _isDragging;
    private IntPtr _hoveredWindow;
    private readonly DispatcherTimer? _windowTrackTimer;
    private readonly DispatcherTimer? _clickTimer;

    public RecordingRegion? SelectedRegion { get; private set; }

    public RegionSelector(VideoCaptureMode mode, AspectRatioPreset? aspectRatio = null)
    {
        InitializeComponent();

        _mode = mode;
        _aspectRatio = aspectRatio;

        // Set up mode-specific UI
        if (_mode == VideoCaptureMode.FullScreen)
        {
            InstructionsPanel.Visibility = Visibility.Collapsed;
            FullScreenPanel.Visibility = Visibility.Visible;
            // Resolution is set in ShowFullScreenSelection after DPI is available
            Cursor = Cursors.Arrow;
        }
        else if (_mode == VideoCaptureMode.Region)
        {
            ModeText.Text = "Region Capture";
            // Update instruction based on whether fixed resolution is selected
            if (_aspectRatio?.FixedWidth.HasValue == true)
            {
                InstructionText.Text = $"Click to place {_aspectRatio.FixedWidth}x{_aspectRatio.FixedHeight} region";
            }
            else
            {
                InstructionText.Text = "Drag to select an area";
            }
            Cursor = Cursors.Cross;
        }
        else
        {
            ModeText.Text = "Window Capture";
            InstructionText.Text = "Click on a window to select it";
            Cursor = Cursors.Arrow;

            // Timer to track window under cursor
            _windowTrackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _windowTrackTimer.Tick += WindowTrackTimer_Tick;
            _windowTrackTimer.Start();

            // Timer to detect mouse clicks (since window is transparent)
            _clickTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _clickTimer.Tick += ClickTimer_Tick;
            _clickTimer.Start();
        }

        // Cover all screens
        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight
        );

        Left = virtualScreen.Left;
        Top = virtualScreen.Top;
        Width = virtualScreen.Width;
        Height = virtualScreen.Height;
        WindowState = WindowState.Normal;

        // For window mode, make the window transparent to mouse input
        if (_mode == VideoCaptureMode.Window)
        {
            SourceInitialized += (s, e) => MakeWindowTransparentToMouse();
        }

        // For full screen mode, show the full screen selection immediately
        if (_mode == VideoCaptureMode.FullScreen)
        {
            Loaded += (s, e) => ShowFullScreenSelection();
        }
    }

    private void ShowFullScreenSelection()
    {
        // Use work area to exclude the taskbar
        var workArea = SystemParameters.WorkArea;
        var left = workArea.Left - SystemParameters.VirtualScreenLeft;
        var top = workArea.Top - SystemParameters.VirtualScreenTop;

        Canvas.SetLeft(SelectionRect, left);
        Canvas.SetTop(SelectionRect, top);
        SelectionRect.Width = workArea.Width;
        SelectionRect.Height = workArea.Height;
        SelectionRect.Visibility = Visibility.Visible;

        // Show physical resolution in the panel
        var dpiScale = VisualTreeHelper.GetDpi(this);
        int physWidth = (int)(workArea.Width * dpiScale.DpiScaleX);
        int physHeight = (int)(workArea.Height * dpiScale.DpiScaleY);
        FullScreenResolution.Text = $"{physWidth} x {physHeight}";

        // Hide the dimension text inside selection rect (shown in center panel instead)
        DimensionText.Visibility = Visibility.Collapsed;
    }

    private void SelectFullScreen()
    {
        // Get physical screen coordinates (accounting for DPI scaling)
        var physicalRect = GetPhysicalWorkArea();
        System.Diagnostics.Debug.WriteLine($"Full screen physical rect: {physicalRect.X},{physicalRect.Y} {physicalRect.Width}x{physicalRect.Height}");

        SelectedRegion = new RecordingRegion
        {
            X = physicalRect.X,
            Y = physicalRect.Y,
            Width = physicalRect.Width,
            Height = physicalRect.Height,
            Mode = VideoCaptureMode.FullScreen
        };
        DialogResult = true;
        Close();
    }

    private (int X, int Y, int Width, int Height) GetPhysicalWorkArea()
    {
        // Get the primary screen's physical resolution using Win32 API
        var hwnd = new WindowInteropHelper(this).Handle;

        // Get DPI scaling factor
        var dpiScale = VisualTreeHelper.GetDpi(this);
        double scaleX = dpiScale.DpiScaleX;
        double scaleY = dpiScale.DpiScaleY;

        // Get logical work area and convert to physical pixels
        var workArea = SystemParameters.WorkArea;
        int physX = (int)(workArea.Left * scaleX);
        int physY = (int)(workArea.Top * scaleY);
        int physWidth = (int)(workArea.Width * scaleX);
        int physHeight = (int)(workArea.Height * scaleY);

        System.Diagnostics.Debug.WriteLine($"DPI Scale: {scaleX}x{scaleY}, Logical: {workArea.Width}x{workArea.Height}, Physical: {physWidth}x{physHeight}");

        return (physX, physY, physWidth, physHeight);
    }

    private void MakeWindowTransparentToMouse()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int extendedStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE,
            extendedStyle | NativeMethods.WS_EX_TRANSPARENT);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
    private const int VK_LBUTTON = 0x01;
    private const int VK_ESCAPE = 0x1B;
    private bool _wasMouseDown;

    private void ClickTimer_Tick(object? sender, EventArgs e)
    {
        // Check for left mouse button
        bool isMouseDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;

        if (!_wasMouseDown && isMouseDown && _hoveredWindow != IntPtr.Zero)
        {
            // Mouse was just clicked, select the hovered window
            SelectCurrentWindow();
        }
        _wasMouseDown = isMouseDown;

        // Check for ESC key
        if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
        {
            DialogResult = false;
            Close();
        }
    }

    private void SelectCurrentWindow()
    {
        if (_hoveredWindow == IntPtr.Zero) return;

        var rect = NativeMethods.GetWindowBounds(_hoveredWindow);
        SelectedRegion = new RecordingRegion
        {
            X = rect.Left,
            Y = rect.Top,
            Width = rect.Width,
            Height = rect.Height,
            WindowHandle = _hoveredWindow,
            WindowTitle = NativeMethods.GetWindowTitle(_hoveredWindow),
            Mode = VideoCaptureMode.Window
        };
        DialogResult = true;
        Close();
    }

    private void WindowTrackTimer_Tick(object? sender, EventArgs e)
    {
        if (_mode != VideoCaptureMode.Window) return;

        NativeMethods.GetCursorPos(out var pt);
        var hwnd = NativeMethods.WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero) return;

        // Get top-level window
        hwnd = NativeMethods.GetTopLevelWindow(hwnd);

        // Skip our own window
        var ourHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == ourHandle) return;

        // Skip desktop and shell
        if (hwnd == NativeMethods.GetDesktopWindow() || hwnd == NativeMethods.GetShellWindow()) return;

        if (hwnd != _hoveredWindow)
        {
            _hoveredWindow = hwnd;
            HighlightWindow(hwnd);
        }
    }

    private void HighlightWindow(IntPtr hwnd)
    {
        var rect = NativeMethods.GetWindowBounds(hwnd);
        var title = NativeMethods.GetWindowTitle(hwnd);

        // Convert screen coordinates to our window coordinates
        var point = PointFromScreen(new Point(rect.Left, rect.Top));

        Canvas.SetLeft(WindowHighlight, point.X);
        Canvas.SetTop(WindowHighlight, point.Y);
        WindowHighlight.Width = rect.Width;
        WindowHighlight.Height = rect.Height;
        WindowTitle.Text = string.IsNullOrEmpty(title) ? "(Untitled)" : title;
        WindowHighlight.Visibility = Visibility.Visible;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (_mode == VideoCaptureMode.FullScreen)
        {
            SelectFullScreen();
            return;
        }

        if (_mode == VideoCaptureMode.Window)
        {
            // Select the hovered window
            if (_hoveredWindow != IntPtr.Zero)
            {
                var rect = NativeMethods.GetWindowBounds(_hoveredWindow);
                SelectedRegion = new RecordingRegion
                {
                    X = rect.Left,
                    Y = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height,
                    WindowHandle = _hoveredWindow,
                    WindowTitle = NativeMethods.GetWindowTitle(_hoveredWindow),
                    Mode = VideoCaptureMode.Window
                };
                DialogResult = true;
                Close();
            }
        }
        else if (_aspectRatio?.FixedWidth.HasValue == true)
        {
            // Fixed resolution mode - click to place
            var currentPoint = e.GetPosition(SelectionCanvas);
            var width = _aspectRatio.FixedWidth.Value;
            var height = _aspectRatio.FixedHeight!.Value;
            var x = currentPoint.X - width / 2;
            var y = currentPoint.Y - height / 2;

            // Convert to screen coordinates
            var screenPoint = SelectionCanvas.PointToScreen(new Point(x, y));

            SelectedRegion = new RecordingRegion
            {
                X = (int)screenPoint.X,
                Y = (int)screenPoint.Y,
                Width = width,
                Height = height,
                Mode = VideoCaptureMode.Region
            };
            DialogResult = true;
            Close();
        }
        else
        {
            // Start drag region selection
            _startPoint = e.GetPosition(SelectionCanvas);
            _isDragging = true;
            InstructionsPanel.Visibility = Visibility.Collapsed;
            PreviewRect.Visibility = Visibility.Collapsed;

            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
            Canvas.SetLeft(SelectionRect, _startPoint.X);
            Canvas.SetTop(SelectionRect, _startPoint.Y);
            SelectionRect.Visibility = Visibility.Visible;

            CaptureMouse();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_mode == VideoCaptureMode.Region)
        {
            var currentPoint = e.GetPosition(SelectionCanvas);

            if (_isDragging)
            {
                UpdateSelectionRect(currentPoint);
            }
            else if (_aspectRatio?.FixedWidth.HasValue == true)
            {
                // Show preview for fixed resolution - center on cursor
                var width = _aspectRatio.FixedWidth.Value;
                var height = _aspectRatio.FixedHeight!.Value;
                var x = currentPoint.X - width / 2;
                var y = currentPoint.Y - height / 2;

                Canvas.SetLeft(PreviewRect, x);
                Canvas.SetTop(PreviewRect, y);
                PreviewRect.Width = width;
                PreviewRect.Height = height;
                PreviewDimensionText.Text = $"{width} x {height}";
                PreviewRect.Visibility = Visibility.Visible;
            }
        }
    }

    private void UpdateSelectionRect(Point currentPoint)
    {
        double x = Math.Min(_startPoint.X, currentPoint.X);
        double y = Math.Min(_startPoint.Y, currentPoint.Y);
        double width = Math.Abs(currentPoint.X - _startPoint.X);
        double height = Math.Abs(currentPoint.Y - _startPoint.Y);

        // Apply aspect ratio constraint if specified
        if (_aspectRatio?.Ratio.HasValue == true)
        {
            var ratio = _aspectRatio.Ratio.Value;
            if (width / height > ratio)
            {
                width = height * ratio;
            }
            else
            {
                height = width / ratio;
            }
        }
        else if (_aspectRatio?.FixedWidth.HasValue == true)
        {
            width = _aspectRatio.FixedWidth.Value;
            height = _aspectRatio.FixedHeight!.Value;
        }

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = width;
        SelectionRect.Height = height;

        DimensionText.Text = $"{(int)width} x {(int)height}";
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_isDragging && _mode == VideoCaptureMode.Region)
        {
            _isDragging = false;
            ReleaseMouseCapture();

            var x = Canvas.GetLeft(SelectionRect);
            var y = Canvas.GetTop(SelectionRect);
            var width = SelectionRect.Width;
            var height = SelectionRect.Height;

            if (width > 10 && height > 10)
            {
                // Convert to screen coordinates
                var screenPoint = SelectionCanvas.PointToScreen(new Point(x, y));

                SelectedRegion = new RecordingRegion
                {
                    X = (int)screenPoint.X,
                    Y = (int)screenPoint.Y,
                    Width = (int)width,
                    Height = (int)height,
                    Mode = VideoCaptureMode.Region
                };
                DialogResult = true;
                Close();
            }
            else
            {
                // Selection too small, reset
                SelectionRect.Visibility = Visibility.Collapsed;
                InstructionsPanel.Visibility = Visibility.Visible;
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
        else if (_mode == VideoCaptureMode.FullScreen && (e.Key == Key.Enter || e.Key == Key.Space))
        {
            SelectFullScreen();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _windowTrackTimer?.Stop();
        _clickTimer?.Stop();
        base.OnClosed(e);
    }
}
