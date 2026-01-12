using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using EmotionInstructor.Models;
using EmotionInstructor.Services;

namespace EmotionInstructor;

public partial class MainWindow : Window
{
    private ScreenCaptureService? _screenCapture;
    private FaceDetectionService? _faceDetection;
    private EmotionDetectionService? _emotionDetection;
    private DispatcherTimer? _monitoringTimer;
    private DispatcherTimer? _flashTimer;
    private ObservableCollection<EmotionLogEntry> _logEntries;
    private bool _isMonitoring = false;
    private bool _isProcessing = false;
    private bool _isAlertActive = false;
    private bool _flashOn = false;
    private int _needsHelpCount = 0;
    private bool _detailedLoggingEnabled = false;

    public MainWindow()
    {
        InitializeComponent();
        _logEntries = new ObservableCollection<EmotionLogEntry>();
        LogDataGrid.ItemsSource = _logEntries;
        SetDetailColumnsVisibility(Visibility.Collapsed);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _logEntries.Clear();
        _needsHelpCount = 0;
        StopAlert();
    }

    private void StopFlashButton_Click(object sender, RoutedEventArgs e)
    {
        StopAlert();
    }

    private void DetailLoggingButton_Click(object sender, RoutedEventArgs e)
    {
        _detailedLoggingEnabled = !_detailedLoggingEnabled;
        DetailLoggingButton.Content = _detailedLoggingEnabled ? "Disable Detailed Log" : "Enable Detailed Log";
        DetailLoggingButton.Background = _detailedLoggingEnabled 
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2E, 0x7D, 0x32))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3F, 0x51, 0xB5));
        SetDetailColumnsVisibility(_detailedLoggingEnabled ? Visibility.Visible : Visibility.Collapsed);
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Status: Initializing...";
            StartButton.IsEnabled = false;
            _needsHelpCount = 0;
            StopAlert();
            StopFlashButton.IsEnabled = false;
            _detailedLoggingEnabled = false;
            DetailLoggingButton.Content = "Enable Detailed Log";
            DetailLoggingButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3F, 0x51, 0xB5));
            SetDetailColumnsVisibility(Visibility.Collapsed);
            
            // Initialize services
            _screenCapture = new ScreenCaptureService();
            
            // Ensure cascade file exists (download if needed)
            StatusText.Text = "Status: Loading face detection model...";
            string cascadePath;
            try
            {
                cascadePath = await CascadeHelper.EnsureCascadeFileExists();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load face detection model:\n\n{ex.Message}\n\nPlease check your internet connection.", 
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StartButton.IsEnabled = true;
                StatusText.Text = "Status: Stopped";
                return;
            }
            
            _faceDetection = new FaceDetectionService(cascadePath);
            
            // Get model paths
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var cnnModelPath = Path.Combine(baseDir, "emotion_cnn.onnx");
            var ferPlusModelPath = Path.Combine(baseDir, "emotion-ferplus-8.onnx");

            // Check if models exist
            if (!File.Exists(cnnModelPath))
            {
                MessageBox.Show($"CNN model not found at: {cnnModelPath}\n\nPlease ensure emotion_cnn.onnx is in the application directory.\n\nYou can copy it using:\nCopy-Item \"..\\Models\\emotion_cnn.onnx\" \"bin\\Debug\\net8.0-windows\\\"",
                    "Model Missing", MessageBoxButton.OK, MessageBoxImage.Error);
                StartButton.IsEnabled = true;
                StatusText.Text = "Status: Stopped";
                return;
            }

            if (!File.Exists(ferPlusModelPath))
            {
                MessageBox.Show($"FerPlus model not found at: {ferPlusModelPath}\n\nPlease ensure emotion-ferplus-8.onnx is in the application directory.\n\nYou can copy it using:\nCopy-Item \"..\\Models\\emotion-ferplus-8.onnx\" \"bin\\Debug\\net8.0-windows\\\"",
                    "Model Missing", MessageBoxButton.OK, MessageBoxImage.Error);
                StartButton.IsEnabled = true;
                StatusText.Text = "Status: Stopped";
                return;
            }

            StatusText.Text = "Status: Loading AI models...";
            try
            {
                _emotionDetection = new EmotionDetectionService(cnnModelPath, ferPlusModelPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load emotion detection models:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}", 
                    "Model Loading Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StartButton.IsEnabled = true;
                StatusText.Text = "Status: Stopped";
                return;
            }

            // Setup timer for 5-second intervals
            _monitoringTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _monitoringTimer.Tick += MonitoringTimer_Tick;
            _monitoringTimer.Start();

            _isMonitoring = true;
            StopButton.IsEnabled = true;
            StatusText.Text = "Status: Monitoring...";

            // Add startup log entry
            _logEntries.Insert(0, new EmotionLogEntry
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                FaceId = 0,
                CnnEmotion = "SYSTEM",
                FerPlusEmotion = "SYSTEM",
                SelectedModel = "N/A",
                FinalEmotion = "Monitoring started successfully",
                Classification = "INFO"
            });

            // Perform first capture immediately
            await Task.Run(async () =>
            {
                await Task.Delay(100); // Small delay to let UI update
                await Dispatcher.InvokeAsync(() => PerformMonitoring());
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error starting monitoring:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}", 
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StopMonitoring();
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopMonitoring();
    }

    private void StopMonitoring()
    {
        _isMonitoring = false;
        _monitoringTimer?.Stop();
        _flashTimer?.Stop();
        
        _emotionDetection?.Dispose();
        _faceDetection?.Dispose();

        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        StopFlashButton.IsEnabled = false;
        StatusText.Text = "Status: Stopped";
        FaceCountText.Text = "Faces Detected: 0";
        StopAlert();
    }

    private void MonitoringTimer_Tick(object? sender, EventArgs e)
    {
        if (_isMonitoring && !_isProcessing)
        {
            PerformMonitoring();
        }
    }

    private async void PerformMonitoring()
    {
        if (_screenCapture == null || _faceDetection == null || _emotionDetection == null)
            return;

        if (_isProcessing)
        {
            // Skip this cycle if still processing
            return;
        }

        _isProcessing = true;

        // Show processing indicator
        StatusText.Text = "Status: Processing...";

        try
        {
            // Run heavy processing on background thread
            var (detectedFaces, timestamp) = await Task.Run(() =>
            {
                try
                {
                    // Capture screen
                    using var screenshot = _screenCapture.CaptureScreen();

                    // Detect faces
                    var faces = _faceDetection.DetectFaces(screenshot);

                    return (faces, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                }
                catch (Exception ex)
                {
                    // Log error and return empty result
                    Dispatcher.Invoke(() =>
                    {
                        var errorEntry = new EmotionLogEntry
                        {
                            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            FaceId = 0,
                            CnnEmotion = "ERROR",
                            FerPlusEmotion = "ERROR",
                            SelectedModel = "N/A",
                            FinalEmotion = ex.Message,
                            Classification = "ERROR"
                        };
                        _logEntries.Insert(0, errorEntry);
                    });
                    
                    return (new List<DetectedFace>(), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                }
            });

            // Update face count on UI thread
            FaceCountText.Text = $"Faces Detected: {detectedFaces.Count}";

            if (detectedFaces.Count == 0)
            {
                // Log that no faces were detected
                var entry = new EmotionLogEntry
                {
                    Timestamp = timestamp,
                    FaceId = 0,
                    CnnEmotion = "N/A",
                    FerPlusEmotion = "N/A",
                    SelectedModel = "N/A",
                    FinalEmotion = "No faces detected",
                    Classification = "N/A"
                };
                
                _logEntries.Insert(0, entry);
                StatusText.Text = "Status: Monitoring...";
                return;
            }

            // Process emotions on background thread
            var entries = await Task.Run(() =>
            {
                var results = new List<EmotionLogEntry>();
                
                for (int i = 0; i < detectedFaces.Count; i++)
                {
                    try
                    {
                        var face = detectedFaces[i];
                        
                        // Predict emotion
                        var (cnnEmotion, ferPlusEmotion, finalEmotion, selectedModel, classification) = 
                            _emotionDetection.PredictEmotion(face.ImageData);

                        if (!_detailedLoggingEnabled)
                        {
                            cnnEmotion = string.Empty;
                            ferPlusEmotion = string.Empty;
                            selectedModel = string.Empty;
                            finalEmotion = string.Empty;
                        }

                        // Create log entry
                        results.Add(new EmotionLogEntry
                        {
                            Timestamp = timestamp,
                            FaceId = i + 1,
                            CnnEmotion = cnnEmotion,
                            FerPlusEmotion = ferPlusEmotion,
                            SelectedModel = selectedModel,
                            FinalEmotion = finalEmotion,
                            Classification = classification
                        });
                    }
                    catch (Exception ex)
                    {
                        // Log error for this face
                        results.Add(new EmotionLogEntry
                        {
                            Timestamp = timestamp,
                            FaceId = i + 1,
                            CnnEmotion = "ERROR",
                            FerPlusEmotion = "ERROR",
                            SelectedModel = "N/A",
                            FinalEmotion = $"Error: {ex.Message}",
                            Classification = "ERROR"
                        });
                    }
                }
                
                return results;
            });

            // Add entries to UI on UI thread
            foreach (var entry in entries)
            {
                _logEntries.Insert(0, entry);

                if (entry.Classification == "Needs Help")
                {
                    _needsHelpCount++;
                }
            }

            if (_needsHelpCount >= 5)
            {
                StartAlert();
            }

            // Limit log entries to prevent memory issues (keep last 1000 entries)
            while (_logEntries.Count > 1000)
            {
                _logEntries.RemoveAt(_logEntries.Count - 1);
            }

            StatusText.Text = "Status: Monitoring...";
        }
        catch (Exception ex)
        {
            var errorEntry = new EmotionLogEntry
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                FaceId = 0,
                CnnEmotion = "FATAL ERROR",
                FerPlusEmotion = "FATAL ERROR",
                SelectedModel = "N/A",
                FinalEmotion = ex.Message,
                Classification = "ERROR"
            };
            _logEntries.Insert(0, errorEntry);
            
            StatusText.Text = "Status: Error occurred";
            
            MessageBox.Show($"Critical error during monitoring:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}", 
                "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isProcessing = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        StopMonitoring();
        base.OnClosed(e);
    }

    private void StartAlert()
    {
        if (_isAlertActive)
            return;

        _isAlertActive = true;
        StopFlashButton.IsEnabled = true;
        AlertBorder.Visibility = Visibility.Visible;

        _flashTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _flashTimer.Tick -= FlashTimer_Tick;
        _flashTimer.Tick += FlashTimer_Tick;
        _flashTimer.Start();
    }

    private void StopAlert()
    {
        _isAlertActive = false;
        _flashOn = false;
        AlertBorder.Visibility = Visibility.Collapsed;
        AlertBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xCD, 0xD2));
        _flashTimer?.Stop();
        StopFlashButton.IsEnabled = false;
    }

    private void FlashTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isAlertActive)
            return;

        _flashOn = !_flashOn;
        var color = _flashOn ? System.Windows.Media.Colors.Red : System.Windows.Media.Color.FromRgb(0xFF, 0xCD, 0xD2);
        AlertBorder.Background = new System.Windows.Media.SolidColorBrush(color);
    }

    private void SetDetailColumnsVisibility(Visibility visibility)
    {
        CnnColumn.Visibility = visibility;
        FerPlusColumn.Visibility = visibility;
        SelectedModelColumn.Visibility = visibility;
        FinalEmotionColumn.Visibility = visibility;
    }
}
