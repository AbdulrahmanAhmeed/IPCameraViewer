using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using IPCameraViewer.Services;
using Microsoft.Maui.Controls;

namespace IPCameraViewer.Models
{
    public class CameraStreamViewModel : INotifyPropertyChanged
    {
        private int _id;
        private string _cameraName = string.Empty;
        private string _url = string.Empty;
        private bool _isRunning;
        private ImageSource? _currentFrame;
        private string _metrics = "Metrics: -";
        private string _motionStatus = "Motion: idle";
        private Color _motionColor = Colors.Gray;
        private float _lastRatio;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string CameraName
        {
            get => _cameraName;
            set => SetProperty(ref _cameraName, value);
        }

        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }

        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }

        public ImageSource? CurrentFrame
        {
            get => _currentFrame;
            set => SetProperty(ref _currentFrame, value);
        }

        public string Metrics
        {
            get => _metrics;
            set => SetProperty(ref _metrics, value);
        }

        public string MotionStatus
        {
            get => _motionStatus;
            set => SetProperty(ref _motionStatus, value);
        }

        public Color MotionColor
        {
            get => _motionColor;
            set => SetProperty(ref _motionColor, value);
        }

        public float LastRatio
        {
            get => _lastRatio;
            set => SetProperty(ref _lastRatio, value);
        }

        public ObservableCollection<string> DetectionLogs { get; } = new();

        public MjpegStreamer? Streamer { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}