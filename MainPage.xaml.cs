using Microsoft.Maui.Controls;
using IPCameraViewer.Services;
using IPCameraViewer.Models;
using System.Net.Http;
using System.Collections.ObjectModel;
using System.Linq;

namespace IPCameraViewer
{
    public partial class MainPage : ContentPage
    {
		private const string MetricsResetText = "Metrics: -";
		private const string MotionIdleText = "Motion: idle";
		private const string MotionDetectedText = "Motion: detected";
		private const string ErrorTitle = "Error";
		private const string OkButtonText = "OK";
		private const string InvalidCameraUrlMessage = "Please enter a valid camera URL.";
		private const string CameraUrlAlreadyAddedMessage = "This camera URL is already added.";
		private const string CameraNameFormat = "Camera {0}";
		private const string AddedStreamFormat = "Added stream: {0}";
		private const string RemovedStreamFormat = "Removed stream: {0}";
		private const string StoppedStreamFormat = "Stopped stream: {0}";
		private const string StartedStreamFormat = "Started stream: {0}";
		private const string AllStreamsStoppedText = "All streams stopped";
		private const string AllLogsClearedText = "All logs cleared";
		private const string StreamErrorTitleFormat = "Stream Error - {0}";
		private const string MetricsFormat = "Metrics: ratio={0:0.000}, changed={1}/{2}";
		private const string MotionDetectedLogFormat = "[{0}] Motion detected (ratio={1:0.000})";
		private const string StatusFormat = "{0} | Active streams: {1}/{2}";
		private const string ReadyStatusText = "Ready";
		private const string EmptyString = "";
		private const int MaxDetectionLogs = 1000;

		private readonly ObservableCollection<CameraStreamViewModel> streams = new();
		private int streamIdCounter = 0;

        public MainPage()
        {
            InitializeComponent();
            this.StreamsCollection.ItemsSource = this.streams;
        }

        private void OnAddStreamClicked(object sender, EventArgs e)
        {
            var cameraUrl = this.CameraUrlEntry.Text?.Trim();
            var cameraName = this.CameraNameEntry.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(cameraUrl))
            {
                if (string.IsNullOrWhiteSpace(cameraName))
                {
                    cameraName = string.Format(MainPage.CameraNameFormat, this.streamIdCounter + 1);
                }

                // Check if URL already exists
                if (!this.streams.Any(s => s.Url == cameraUrl))
                {
                    var streamViewModel = new CameraStreamViewModel
                    {
                        Id = this.streamIdCounter++,
                        CameraName = cameraName,
                        Url = cameraUrl
                    };

                    this.streams.Add(streamViewModel);
                    this.StartStream(streamViewModel);

                    // Clear inputs
                    this.CameraUrlEntry.Text = MainPage.EmptyString;
                    this.CameraNameEntry.Text = MainPage.EmptyString;

                    this.UpdateStatus(string.Format(MainPage.AddedStreamFormat, cameraName));
                }
                else
                {
                    this.DisplayAlert(MainPage.ErrorTitle, MainPage.CameraUrlAlreadyAddedMessage, MainPage.OkButtonText);
                }
            }
            else
            {
                this.DisplayAlert(MainPage.ErrorTitle, MainPage.InvalidCameraUrlMessage, MainPage.OkButtonText);
            }
        }

        private void OnRemoveStreamClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is int id)
            {
                var stream = this.streams.FirstOrDefault(s => s.Id == id);
                if (stream != null)
                {
                    this.StopStream(stream);
                    this.streams.Remove(stream);
                    this.UpdateStatus(string.Format(MainPage.RemovedStreamFormat, stream.CameraName));
                }
            }
        }

        private void OnToggleStreamClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is int id)
            {
                var stream = this.streams.FirstOrDefault(s => s.Id == id);
                if (stream != null)
                {
                    if (stream.IsRunning)
                    {
                        this.StopStream(stream);
                        this.UpdateStatus(string.Format(MainPage.StoppedStreamFormat, stream.CameraName));
                    }
                    else
                    {
                        this.StartStream(stream);
                        this.UpdateStatus(string.Format(MainPage.StartedStreamFormat, stream.CameraName));
                    }
                }
            }
        }

        private void OnStopAllClicked(object sender, EventArgs e)
        {
            foreach (var stream in this.streams)
            {
                this.StopStream(stream);
            }
            this.UpdateStatus(MainPage.AllStreamsStoppedText);
        }

        private void OnClearStreamLogsClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is int id)
            {
                var stream = this.streams.FirstOrDefault(s => s.Id == id);
                if (stream != null)
                {
                    stream.DetectionLogs.Clear();
                }
            }
        }

        private void OnClearAllLogsClicked(object sender, EventArgs e)
        {
            foreach (var stream in this.streams)
            {
                stream.DetectionLogs.Clear();
            }
            this.UpdateStatus(MainPage.AllLogsClearedText);
        }

        private void StartStream(CameraStreamViewModel streamViewModel)
        {
            if (streamViewModel.Streamer != null)
            {
                this.StopStream(streamViewModel);
            }

            // Convert percentage to ratio (1.5% = 0.015)
            float thresholdRatio = (float)(streamViewModel.MotionThresholdPercent / 100.0);
            var streamer = new MjpegStreamer(new HttpClient(), differenceThresholdRatio: thresholdRatio);
            streamer.FrameReceived += (jpegBytes) => this.OnFrameReceived(streamViewModel, jpegBytes);
            streamer.Metrics += (ratio, changed, total) => this.OnMetrics(streamViewModel, ratio, changed, total);
            streamer.MotionDetected += () => this.OnMotion(streamViewModel);
            streamer.Error += (message) => this.OnError(streamViewModel, message);

            streamViewModel.Streamer = streamer;
            streamViewModel.IsRunning = true;
            streamer.Start(streamViewModel.Url);
        }

        private async void StopStream(CameraStreamViewModel streamViewModel)
        {
            if (streamViewModel.Streamer != null)
            {
                var streamer = streamViewModel.Streamer;
                streamViewModel.Streamer = null;
                streamViewModel.IsRunning = false;

                try
                {
                    await streamer.DisposeAsync();
                }
                catch { }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    streamViewModel.CurrentFrame = null;
                    streamViewModel.Metrics = MainPage.MetricsResetText;
                    streamViewModel.MotionStatus = MainPage.MotionIdleText;
                    streamViewModel.MotionColor = Colors.Gray;
                });
            }
        }

        private void OnFrameReceived(CameraStreamViewModel streamViewModel, byte[] jpegBytes)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                streamViewModel.CurrentFrame = ImageSource.FromStream(() => new MemoryStream(jpegBytes));
            });
        }

        private void OnMetrics(CameraStreamViewModel streamViewModel, float ratio, int changed, int total)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                streamViewModel.Metrics = string.Format(MainPage.MetricsFormat, ratio, changed, total);
                streamViewModel.LastRatio = ratio;
            });
        }

        private void OnMotion(CameraStreamViewModel streamViewModel)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                streamViewModel.MotionStatus = MainPage.MotionDetectedText;
                streamViewModel.MotionColor = Colors.OrangeRed;

                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                streamViewModel.DetectionLogs.Add(string.Format(MainPage.MotionDetectedLogFormat, timestamp, streamViewModel.LastRatio));

                if (streamViewModel.DetectionLogs.Count > MainPage.MaxDetectionLogs)
                {
                    streamViewModel.DetectionLogs.RemoveAt(0);
                }
            });
        }

        private void OnError(CameraStreamViewModel streamViewModel, string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                this.DisplayAlert(string.Format(MainPage.StreamErrorTitleFormat, streamViewModel.CameraName), message, MainPage.OkButtonText);
                streamViewModel.IsRunning = false;
            });
        }

        private void UpdateStatus(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                this.StatusLabel.Text = string.Format(MainPage.StatusFormat, message, this.streams.Count(s => s.IsRunning), this.streams.Count);
            });
        }

        private void OnThresholdChanged(object sender, ValueChangedEventArgs e)
        {
            if (sender is Slider slider && slider.BindingContext is CameraStreamViewModel streamViewModel)
            {
                // Update the view model property (binding will handle this, but we also need to update the streamer)
                streamViewModel.MotionThresholdPercent = e.NewValue;
                
                // Update the streamer's threshold if it's running
                if (streamViewModel.Streamer != null)
                {
                    float thresholdRatio = (float)(e.NewValue / 100.0);
                    streamViewModel.Streamer.DifferenceThresholdRatio = thresholdRatio;
                }
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            foreach (var stream in this.streams)
            {
                this.StopStream(stream);
            }
        }
    }
}