using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
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

            var recordRegionItem = new MenuItem { Header = "Record Region", FontWeight = FontWeights.SemiBold };
            recordRegionItem.Click += TrayMenu_RecordRegion_Click;
            contextMenu.Items.Add(recordRegionItem);

            var recordWindowItem = new MenuItem { Header = "Record Window" };
            recordWindowItem.Click += TrayMenu_RecordWindow_Click;
            contextMenu.Items.Add(recordWindowItem);

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
                    BtnNewRegion.IsEnabled = true;
                    BtnNewWindow.IsEnabled = true;
                    AspectRatioCombo.IsEnabled = true;
                    BtnPause.IsEnabled = false;
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
                    BtnNewRegion.IsEnabled = false;
                    BtnNewWindow.IsEnabled = false;
                    AspectRatioCombo.IsEnabled = false;
                    BtnPause.IsEnabled = true;
                    BtnStop.IsEnabled = true;
                    RecordingIndicator.Visibility = Visibility.Visible;
                    _blinkTimer.Start();
                    Show();
                    break;

                case RecordingState.Stopping:
                    BtnPause.IsEnabled = false;
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
        var aspectRatio = AspectRatioCombo.SelectedItem as AspectRatioPreset;

        // Show region selector
        Hide();
        await Task.Delay(100); // Let the window hide

        var selector = new RegionSelector(mode, aspectRatio);
        var result = selector.ShowDialog();

        if (result == true && selector.SelectedRegion != null)
        {
            var region = selector.SelectedRegion;

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

    private async Task StopRecording()
    {
        var recordingResult = await _recordingController.StopRecordingAsync();

        if (recordingResult != null && File.Exists(recordingResult.TempFilePath))
        {
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
                    // Move temp file to selected location
                    if (File.Exists(saveDialog.FileName))
                    {
                        File.Delete(saveDialog.FileName);
                    }
                    File.Move(recordingResult.TempFilePath, saveDialog.FileName);

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

    #region System Tray

    private bool _isExiting;

    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        ShowWindow();
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
        if (_recordingController.State == RecordingState.Recording)
        {
            var result = MessageBox.Show(
                "Recording is in progress. Stop and exit?",
                "Video Snip",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
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
        _trayIcon?.Dispose();
        _recordingController.Dispose();
        base.OnClosed(e);
    }
}
