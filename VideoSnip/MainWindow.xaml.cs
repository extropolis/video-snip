using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using VideoSnip.Helpers;
using VideoSnip.Models;
using VideoSnip.Services;
using VideoSnip.Views;

namespace VideoSnip;

public partial class MainWindow : Window
{
    private readonly RecordingController _recordingController;
    private readonly DispatcherTimer _blinkTimer;
    private TaskbarIcon? _trayIcon;
    private bool _blinkState;
    private bool _isPaused;
    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;
    private VideoCaptureMode _currentCaptureMode;
    private RecordingRegion? _currentRegion;

    // Tray menu items
    private MenuItem? _trayRecordFullScreen;
    private MenuItem? _trayRecordRegion;
    private MenuItem? _trayRecordWindow;
    private MenuItem? _trayPause;
    private MenuItem? _trayStop;
    private Separator? _trayRecordingSeparator;

    public MainWindow()
    {
        InitializeComponent();

        _recordingController = new RecordingController();
        _recordingController.StateChanged += OnStateChanged;
        _recordingController.DurationUpdated += OnDurationUpdated;

        // Initialize aspect ratio dropdown
        AspectRatioCombo.ItemsSource = AspectRatioPreset.Presets;
        AspectRatioCombo.DisplayMemberPath = "Name";
        AspectRatioCombo.SelectedIndex = 0;

        // Blink timer for recording indicator
        _blinkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _blinkTimer.Tick += (s, e) =>
        {
            _blinkState = !_blinkState;
            RecordingIndicator.Opacity = _blinkState ? 1 : 0.3;
        };

        // Set up keyboard shortcuts
        KeyDown += MainWindow_KeyDown;

        // Minimize to tray instead of taskbar
        StateChanged += (s, e) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                WindowState = WindowState.Normal;
            }
        };

        // Initialize system tray icon
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        try
        {
            _trayIcon = new TaskbarIcon
            {
                ToolTipText = "Video Snip - Right-click for options",
                IconSource = new BitmapImage(new Uri("pack://application:,,,/icon.ico", UriKind.Absolute))
            };

            // Create context menu
            var contextMenu = new ContextMenu();

            // Recording controls (hidden by default, shown during recording)
            _trayPause = new MenuItem { Header = "⏸  Pause Recording", FontWeight = FontWeights.SemiBold, Visibility = Visibility.Collapsed };
            _trayPause.Click += TrayMenu_Pause_Click;
            contextMenu.Items.Add(_trayPause);

            _trayStop = new MenuItem { Header = "⏹  Stop Recording", Visibility = Visibility.Collapsed };
            _trayStop.Click += TrayMenu_Stop_Click;
            contextMenu.Items.Add(_trayStop);

            _trayRecordingSeparator = new Separator { Visibility = Visibility.Collapsed };
            contextMenu.Items.Add(_trayRecordingSeparator);

            // Record options (shown by default, hidden during recording)
            _trayRecordFullScreen = new MenuItem { Header = "Record Full Screen", FontWeight = FontWeights.SemiBold };
            _trayRecordFullScreen.Click += TrayMenu_RecordFullScreen_Click;
            contextMenu.Items.Add(_trayRecordFullScreen);

            _trayRecordRegion = new MenuItem { Header = "Record Region" };
            _trayRecordRegion.Click += TrayMenu_RecordRegion_Click;
            contextMenu.Items.Add(_trayRecordRegion);

            _trayRecordWindow = new MenuItem { Header = "Record Window" };
            _trayRecordWindow.Click += TrayMenu_RecordWindow_Click;
            contextMenu.Items.Add(_trayRecordWindow);

            contextMenu.Items.Add(new Separator());

            var showItem = new MenuItem { Header = "Show Video Snip" };
            showItem.Click += TrayMenu_Show_Click;
            contextMenu.Items.Add(showItem);

            contextMenu.Items.Add(new Separator());

            var exitItem = new MenuItem { Header = "Exit" };
            exitItem.Click += TrayMenu_Exit_Click;
            contextMenu.Items.Add(exitItem);

            _trayIcon.ContextMenu = contextMenu;
            _trayIcon.TrayMouseDoubleClick += TrayIcon_TrayMouseDoubleClick;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize tray icon: {ex.Message}");
        }
    }

    private void UpdateTrayMenuForRecording(bool isRecording)
    {
        if (_trayIcon == null) return;

        if (isRecording)
        {
            // Show recording controls
            if (_trayPause != null) _trayPause.Visibility = Visibility.Visible;
            if (_trayStop != null) _trayStop.Visibility = Visibility.Visible;
            if (_trayRecordingSeparator != null) _trayRecordingSeparator.Visibility = Visibility.Visible;

            // Hide record options
            if (_trayRecordFullScreen != null) _trayRecordFullScreen.Visibility = Visibility.Collapsed;
            if (_trayRecordRegion != null) _trayRecordRegion.Visibility = Visibility.Collapsed;
            if (_trayRecordWindow != null) _trayRecordWindow.Visibility = Visibility.Collapsed;

            _trayIcon.ToolTipText = "Video Snip - Recording...";
        }
        else
        {
            // Hide recording controls
            if (_trayPause != null) _trayPause.Visibility = Visibility.Collapsed;
            if (_trayStop != null) _trayStop.Visibility = Visibility.Collapsed;
            if (_trayRecordingSeparator != null) _trayRecordingSeparator.Visibility = Visibility.Collapsed;

            // Show record options
            if (_trayRecordFullScreen != null) _trayRecordFullScreen.Visibility = Visibility.Visible;
            if (_trayRecordRegion != null) _trayRecordRegion.Visibility = Visibility.Visible;
            if (_trayRecordWindow != null) _trayRecordWindow.Visibility = Visibility.Visible;

            _trayIcon.ToolTipText = "Video Snip - Right-click for options";
        }

        // Update pause button text
        UpdateTrayPauseButton();
    }

    private void UpdateTrayPauseButton()
    {
        if (_trayPause != null)
        {
            _trayPause.Header = _isPaused ? "▶  Resume Recording" : "⏸  Pause Recording";
        }
    }

    private async void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (_recordingController.State == RecordingState.Recording)
        {
            if (e.Key == Key.Space || e.Key == Key.Enter || e.Key == Key.Escape)
            {
                e.Handled = true;
                await StopRecording();
            }
        }
    }

    private void OnStateChanged(RecordingState state)
    {
        Dispatcher.Invoke(() =>
        {
            switch (state)
            {
                case RecordingState.Idle:
                    UnregisterGlobalHotkeys();
                    UpdateTrayMenuForRecording(false);
                    BtnFullScreen.IsEnabled = true;
                    BtnNewRegion.IsEnabled = true;
                    BtnNewWindow.IsEnabled = true;
                    AspectRatioCombo.IsEnabled = true;
                    BtnPause.IsEnabled = false;
                    BtnRestart.IsEnabled = false;
                    BtnStop.IsEnabled = false;
                    RecordingIndicator.Visibility = Visibility.Collapsed;
                    DurationText.Text = "";
                    _blinkTimer.Stop();
                    _isPaused = false;
                    UpdatePauseButton();
                    Show();
                    break;

                case RecordingState.Selecting:
                    Hide();
                    break;

                case RecordingState.Recording:
                    RegisterGlobalHotkeys();
                    BtnFullScreen.IsEnabled = false;
                    BtnNewRegion.IsEnabled = false;
                    BtnNewWindow.IsEnabled = false;
                    AspectRatioCombo.IsEnabled = false;
                    BtnPause.IsEnabled = true;
                    BtnRestart.IsEnabled = true;
                    BtnStop.IsEnabled = true;
                    RecordingIndicator.Visibility = Visibility.Visible;
                    _blinkTimer.Start();
                    // Only minimize to tray for full screen recording
                    if (_currentCaptureMode == VideoCaptureMode.FullScreen)
                    {
                        Hide();
                    }
                    else
                    {
                        Show();
                    }
                    UpdateTrayMenuForRecording(true);
                    break;

                case RecordingState.Stopping:
                    BtnPause.IsEnabled = false;
                    BtnRestart.IsEnabled = false;
                    BtnStop.IsEnabled = false;
                    _blinkTimer.Stop();
                    break;
            }
        });
    }

    private void OnDurationUpdated(TimeSpan duration)
    {
        Dispatcher.Invoke(() =>
        {
            DurationText.Text = duration.ToString(@"mm\:ss");
        });
    }

    private async void BtnFullScreen_Click(object sender, RoutedEventArgs e)
    {
        await StartCapture(VideoCaptureMode.FullScreen);
    }

    private async void BtnNewRegion_Click(object sender, RoutedEventArgs e)
    {
        await StartCapture(VideoCaptureMode.Region);
    }

    private async void BtnNewWindow_Click(object sender, RoutedEventArgs e)
    {
        await StartCapture(VideoCaptureMode.Window);
    }

    private async Task StartCapture(VideoCaptureMode mode)
    {
        _currentCaptureMode = mode;
        var aspectRatio = AspectRatioCombo.SelectedItem as AspectRatioPreset;

        // Show region selector
        Hide();
        await Task.Delay(100); // Let the window hide

        var selector = new RegionSelector(mode, aspectRatio);
        var result = selector.ShowDialog();

        if (result == true && selector.SelectedRegion != null)
        {
            var region = selector.SelectedRegion;
            _currentRegion = region; // Store for restart functionality

            // Start recording
            var (success, errorDetail) = await _recordingController.StartRecordingAsync(region);

            if (!success)
            {
                Show();
                var message = "Failed to start recording.";
                if (!string.IsNullOrEmpty(errorDetail))
                    message += $"\n\nDetails: {errorDetail}";
                else
                    message += "\n\nMake sure you're running Windows 10 version 1803 or later.";

                MessageBox.Show(
                    message,
                    "Video Snip",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        else
        {
            Show();
        }
    }

    private void BtnPause_Click(object sender, RoutedEventArgs e)
    {
        if (_isPaused)
        {
            _recordingController.Resume();
            _isPaused = false;
            _blinkTimer.Start();
        }
        else
        {
            _recordingController.Pause();
            _isPaused = true;
            _blinkTimer.Stop();
            RecordingIndicator.Opacity = 0.5;
        }
        UpdatePauseButton();
    }

    private async void BtnRestart_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRegion == null) return;

        // Store the region before stopping (stop will clear it if we don't)
        var regionToRestart = _currentRegion;

        // Cancel the current recording (discard without saving)
        await _recordingController.CancelRecordingAsync();

        // Wait a bit for cleanup
        await Task.Delay(200);

        // Start a new recording with the same region
        var (success, errorDetail) = await _recordingController.StartRecordingAsync(regionToRestart);

        if (!success)
        {
            Show();
            var message = "Failed to restart recording.";
            if (!string.IsNullOrEmpty(errorDetail))
                message += $"\n\nDetails: {errorDetail}";

            MessageBox.Show(
                message,
                "Video Snip",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        else
        {
            _currentRegion = regionToRestart;
        }
    }

    private void UpdatePauseButton()
    {
        if (_isPaused)
        {
            PauseIcon.Text = "\uE768"; // Play icon
            PauseText.Text = "Resume";
        }
        else
        {
            PauseIcon.Text = "\uE769"; // Pause icon
            PauseText.Text = "Pause";
        }
    }

    private async void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        await StopRecording();
    }

    private void BtnExit_Click(object sender, RoutedEventArgs e)
    {
        _isExiting = true;
        Close();
    }

    private async Task StopRecording()
    {
        var recordingResult = await _recordingController.StopRecordingAsync();

        if (recordingResult != null && File.Exists(recordingResult.TempFilePath))
        {
            // Wait for file to be ready (not locked)
            await WaitForFileReady(recordingResult.TempFilePath);

            // Show Save File dialog
            var saveDialog = new SaveFileDialog
            {
                Title = "Save Recording",
                Filter = "MP4 Video (*.mp4)|*.mp4|All Files (*.*)|*.*",
                DefaultExt = ".mp4",
                FileName = recordingResult.SuggestedFileName,
                InitialDirectory = recordingResult.DefaultFolder,
                OverwritePrompt = true
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    // Move temp file to selected location (with retry for file lock)
                    if (File.Exists(saveDialog.FileName))
                    {
                        File.Delete(saveDialog.FileName);
                    }

                    // Use FileStream with sharing to handle any lingering handles
                    const int maxRetries = 10;
                    Exception? lastException = null;
                    bool success = false;
                    for (int i = 0; i < maxRetries; i++)
                    {
                        try
                        {
                            // Use FileStream with ReadWrite sharing to allow reading even if file has other handles
                            using (var sourceStream = new FileStream(
                                recordingResult.TempFilePath,
                                FileMode.Open,
                                FileAccess.Read,
                                FileShare.ReadWrite | FileShare.Delete))
                            using (var destStream = new FileStream(
                                saveDialog.FileName,
                                FileMode.Create,
                                FileAccess.Write,
                                FileShare.None))
                            {
                                await sourceStream.CopyToAsync(destStream);
                            }
                            success = true;

                            // Try to delete temp file (may fail, that's ok)
                            try { File.Delete(recordingResult.TempFilePath); } catch { }
                            break;
                        }
                        catch (IOException ex) when (i < maxRetries - 1)
                        {
                            lastException = ex;
                            // Force GC and wait before retry
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            await Task.Delay(300);
                        }
                    }

                    if (!success && lastException != null)
                    {
                        throw lastException;
                    }

                    // Get file size
                    var fileInfo = new FileInfo(saveDialog.FileName);
                    var fileSizeStr = FormatFileSize(fileInfo.Length);

                    // Show success notification with file size
                    var openResult = MessageBox.Show(
                        $"Recording saved to:\n{saveDialog.FileName}\n\nDuration: {recordingResult.Duration:mm\\:ss}\nFile size: {fileSizeStr}\n\nOpen file location?",
                        "Video Snip",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (openResult == MessageBoxResult.Yes)
                    {
                        Process.Start("explorer.exe", $"/select,\"{saveDialog.FileName}\"");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to save recording:\n{ex.Message}",
                        "Video Snip",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            else
            {
                // User cancelled, delete temp file
                try
                {
                    File.Delete(recordingResult.TempFilePath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private static async Task WaitForFileReady(string filePath, int maxWaitMs = 30000)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        long lastSize = -1;
        int stableCount = 0;

        Debug.WriteLine($"Waiting for file to be ready: {filePath}");

        while (stopwatch.ElapsedMilliseconds < maxWaitMs)
        {
            try
            {
                // Check if file size is stable (not still being written to)
                var fileInfo = new FileInfo(filePath);
                long currentSize = fileInfo.Length;

                if (currentSize == lastSize && currentSize > 0)
                {
                    stableCount++;
                    // Wait for 3 consecutive stable checks before trying exclusive access
                    if (stableCount >= 3)
                    {
                        // Try to open the file with exclusive access
                        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            Debug.WriteLine($"File ready after {stopwatch.ElapsedMilliseconds}ms, size: {currentSize}");
                            return;
                        }
                    }
                }
                else
                {
                    stableCount = 0;
                    Debug.WriteLine($"File still growing: {currentSize} bytes");
                }

                lastSize = currentSize;
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"File still locked: {ex.Message}");
                stableCount = 0;
            }

            // Force GC and wait
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(300);
        }

        Debug.WriteLine($"File not ready after {maxWaitMs}ms, proceeding anyway");
    }

    #region System Tray

    private bool _isExiting;

    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        ShowWindow();
    }

    private void TrayMenu_Pause_Click(object sender, RoutedEventArgs e)
    {
        BtnPause_Click(sender, e);
        UpdateTrayPauseButton();
    }

    private async void TrayMenu_Stop_Click(object sender, RoutedEventArgs e)
    {
        await StopRecording();
    }

    private async void TrayMenu_RecordFullScreen_Click(object sender, RoutedEventArgs e)
    {
        await StartCapture(VideoCaptureMode.FullScreen);
    }

    private async void TrayMenu_RecordRegion_Click(object sender, RoutedEventArgs e)
    {
        await StartCapture(VideoCaptureMode.Region);
    }

    private async void TrayMenu_RecordWindow_Click(object sender, RoutedEventArgs e)
    {
        await StartCapture(VideoCaptureMode.Window);
    }

    private void TrayMenu_Show_Click(object sender, RoutedEventArgs e)
    {
        ShowWindow();
    }

    private void TrayMenu_Exit_Click(object sender, RoutedEventArgs e)
    {
        _isExiting = true;
        Close();
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    #endregion

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // If truly exiting and recording, prompt to stop
        if (_isExiting && _recordingController.State == RecordingState.Recording)
        {
            var result = MessageBox.Show(
                "Recording is in progress. Stop and exit?",
                "Video Snip",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                _isExiting = false;
                e.Cancel = true;
                return;
            }

            _ = StopRecording();
        }

        // Minimize to tray instead of closing (unless exiting)
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        UnregisterGlobalHotkeys();
        _hwndSource?.RemoveHook(WndProc);
        _trayIcon?.Dispose();
        _recordingController.Dispose();
        base.OnClosed(e);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowHandle = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int hotkeyId = wParam.ToInt32();
            if (hotkeyId == NativeMethods.HOTKEY_STOP)
            {
                handled = true;
                Dispatcher.BeginInvoke(async () => await StopRecording());
            }
            else if (hotkeyId == NativeMethods.HOTKEY_PAUSE)
            {
                handled = true;
                Dispatcher.BeginInvoke(() => BtnPause_Click(this, new RoutedEventArgs()));
            }
        }
        return IntPtr.Zero;
    }

    private void RegisterGlobalHotkeys()
    {
        if (_windowHandle == IntPtr.Zero) return;

        // Ctrl+Shift+S for Stop
        NativeMethods.RegisterHotKey(_windowHandle, NativeMethods.HOTKEY_STOP,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
            NativeMethods.VK_S);

        // Ctrl+Shift+P for Pause
        NativeMethods.RegisterHotKey(_windowHandle, NativeMethods.HOTKEY_PAUSE,
            NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
            NativeMethods.VK_P);
    }

    private void UnregisterGlobalHotkeys()
    {
        if (_windowHandle == IntPtr.Zero) return;

        NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.HOTKEY_STOP);
        NativeMethods.UnregisterHotKey(_windowHandle, NativeMethods.HOTKEY_PAUSE);
    }
}
