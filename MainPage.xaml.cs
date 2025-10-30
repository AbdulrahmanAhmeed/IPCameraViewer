using Microsoft.Maui.Controls;
using IPCameraViewer.Services;

using System.Net.Http;
using System.Collections.ObjectModel;

namespace IPCameraViewer
{
    public partial class MainPage : ContentPage
    {
        private MjpegStreamer? _streamer;
        private readonly ObservableCollection<string> _detectionLogs = new();
        private float _lastRatio;

        public MainPage()
        {
            InitializeComponent();
            LogList.ItemsSource = _detectionLogs;
        }

        private void OnStartClicked(object sender, EventArgs e)
        {
            var cameraUrl = CameraUrlEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(cameraUrl))
            {
                DisplayAlert("Error", "Please enter a valid camera URL.", "OK");
                return;
            }

            StopStreamer();

            _streamer = new MjpegStreamer(new HttpClient());
            _streamer.FrameReceived += OnFrameReceived;
            _streamer.Metrics += OnMetrics;
            _streamer.MotionDetected += OnMotion;
            _streamer.Error += OnError;
            _streamer.Start(cameraUrl);
        }

        private void OnStopClicked(object sender, EventArgs e)
        {
            StopStreamer();
        }

        private async void StopStreamer()
        {
            if (_streamer != null)
            {
                var s = _streamer;
                _streamer = null;
                try { await s.DisposeAsync(); } catch { }
            }
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StreamImage.Source = null;
                MetricsLabel.Text = "Metrics: -";
                MotionLabel.Text = "Motion: idle";
                MotionLabel.TextColor = Colors.Gray;
            });
        }

        private void OnFrameReceived(byte[] jpegBytes)
        {
            // Update Image control from bytes
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StreamImage.Source = ImageSource.FromStream(() => new MemoryStream(jpegBytes));
            });
        }

        private void OnMetrics(float ratio, int changed, int total)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                MetricsLabel.Text = $"Metrics: ratio={ratio:0.000}, changed={changed}/{total}";
                _lastRatio = ratio;
            });
        }

        private void OnMotion()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                MotionLabel.Text = "Motion: detected";
                MotionLabel.TextColor = Colors.OrangeRed;
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                _detectionLogs.Add($"[{timestamp}] Motion detected (ratio={_lastRatio:0.000})");
                if (_detectionLogs.Count > 1000)
                {
                    _detectionLogs.RemoveAt(0);
                }
                if (_detectionLogs.Count > 0)
                {
                    LogList.ScrollTo(_detectionLogs[^1], position: ScrollToPosition.End, animate: true);
                }
            });
        }

        private void OnError(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DisplayAlert("Stream Error", message, "OK");
            });
        }

        private void OnClearLogsClicked(object sender, EventArgs e)
        {
            _detectionLogs.Clear();
        }
    }
}
