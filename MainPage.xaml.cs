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

		private readonly ObservableCollection<CameraStreamViewModel> streams = new();
		private int streamIdCounter = 0;

        public MainPage()
        {
            InitializeComponent();
			StreamsCollection.ItemsSource = this.streams;
        }

        private void OnAddStreamClicked(object sender, EventArgs e)
        {
            var cameraUrl = CameraUrlEntry.Text?.Trim();
            var cameraName = CameraNameEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(cameraUrl))
            {
                DisplayAlert("Error", "Please enter a valid camera URL.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(cameraName))
            {
				cameraName = $"Camera {this.streamIdCounter + 1}";
            }

            // Check if URL already exists
            if (this.streams.Any(s => s.Url == cameraUrl))
            {
                DisplayAlert("Error", "This camera URL is already added.", "OK");
                return;
            }

            var streamViewModel = new CameraStreamViewModel
            {
				Id = this.streamIdCounter++,
                CameraName = cameraName,
                Url = cameraUrl
            };

			this.streams.Add(streamViewModel);
            StartStream(streamViewModel);

            // Clear inputs
            CameraUrlEntry.Text = string.Empty;
            CameraNameEntry.Text = string.Empty;

            UpdateStatus($"Added stream: {cameraName}");
        }

        private void OnRemoveStreamClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is int id)
            {
                var stream = this.streams.FirstOrDefault(s => s.Id == id);
                if (stream != null)
                {
                    StopStream(stream);
					this.streams.Remove(stream);
                    UpdateStatus($"Removed stream: {stream.CameraName}");
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
                        StopStream(stream);
                        UpdateStatus($"Stopped stream: {stream.CameraName}");
                    }
                    else
                    {
                        StartStream(stream);
                        UpdateStatus($"Started stream: {stream.CameraName}");
                    }
                }
            }
        }

        private void OnStopAllClicked(object sender, EventArgs e)
        {
            foreach (var stream in this.streams)
            {
                StopStream(stream);
            }
            UpdateStatus("All streams stopped");
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
            UpdateStatus("All logs cleared");
        }

        private void StartStream(CameraStreamViewModel streamViewModel)
        {
            if (streamViewModel.Streamer != null)
            {
                StopStream(streamViewModel);
            }

            var streamer = new MjpegStreamer(new HttpClient());
            streamer.FrameReceived += (jpegBytes) => OnFrameReceived(streamViewModel, jpegBytes);
            streamer.Metrics += (ratio, changed, total) => OnMetrics(streamViewModel, ratio, changed, total);
            streamer.MotionDetected += () => OnMotion(streamViewModel);
            streamer.Error += (message) => OnError(streamViewModel, message);

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
                streamViewModel.Metrics = $"Metrics: ratio={ratio:0.000}, changed={changed}/{total}";
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
                streamViewModel.DetectionLogs.Add($"[{timestamp}] Motion detected (ratio={streamViewModel.LastRatio:0.000})");

                if (streamViewModel.DetectionLogs.Count > 1000)
                {
                    streamViewModel.DetectionLogs.RemoveAt(0);
                }
            });
        }

        private void OnError(CameraStreamViewModel streamViewModel, string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DisplayAlert($"Stream Error - {streamViewModel.CameraName}", message, "OK");
                streamViewModel.IsRunning = false;
            });
        }

        private void UpdateStatus(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
				StatusLabel.Text = $"{message} | Active streams: {this.streams.Count(s => s.IsRunning)}/{this.streams.Count}";
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            foreach (var stream in this.streams)
            {
                StopStream(stream);
            }
        }
    }
}