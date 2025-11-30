using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using IPCameraViewer.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace IPCameraViewer.Models
{
    public class CameraStreamViewModel : INotifyPropertyChanged
    {
        private const string DefaultMetricsText = "Metrics: -";
        private const string DefaultMotionIdleText = "Motion: idle";
        private const double DefaultMotionThresholdPercent = 1.5;
        private const double DefaultSoundVolume = 1.0;
        private const bool DefaultSoundEnabled = true;
        private const int DefaultRecordingBeforeSeconds = 5;
        private const int DefaultRecordingAfterSeconds = 10;
        private const bool DefaultRecordingEnabled = false;
        private const bool DefaultRecordingGif = false;
        private const bool DefaultRecordingPng = false;
        private const bool DefaultRecordingMp4 = true;
        private const string SoundVolumeKeyFormat = "Camera_{0}_SoundVolume";
        private const string SoundEnabledKeyFormat = "Camera_{0}_SoundEnabled";
        private const string SoundFilePathKeyFormat = "Camera_{0}_SoundFilePath";
        private const string RecordingEnabledKeyFormat = "Camera_{0}_RecordingEnabled";
        private const string RecordingBeforeSecondsKeyFormat = "Camera_{0}_RecordingBeforeSeconds";
        private const string RecordingAfterSecondsKeyFormat = "Camera_{0}_RecordingAfterSeconds";
        private const string RecordingGifKeyFormat = "Camera_{0}_RecordingGif";
        private const string RecordingPngKeyFormat = "Camera_{0}_RecordingPng";
        private const string RecordingMp4KeyFormat = "Camera_{0}_RecordingMp4";
        private const string RecordingOutputPathKeyFormat = "Camera_{0}_RecordingOutputPath";

        private int id;
        private string cameraName = string.Empty;
        private string url = string.Empty;
        private bool isRunning;
        private ImageSource? currentFrame;
        private string metrics = CameraStreamViewModel.DefaultMetricsText;
        private string motionStatus = CameraStreamViewModel.DefaultMotionIdleText;
        private Color motionColor = Colors.Gray;
        private float lastRatio;
        private double motionThresholdPercent = CameraStreamViewModel.DefaultMotionThresholdPercent;
        private double soundVolume = CameraStreamViewModel.DefaultSoundVolume;
        private bool soundEnabled = CameraStreamViewModel.DefaultSoundEnabled;
        private string? soundFilePath;
        private bool recordingEnabled = CameraStreamViewModel.DefaultRecordingEnabled;
        private int recordingBeforeSeconds = CameraStreamViewModel.DefaultRecordingBeforeSeconds;
        private int recordingAfterSeconds = CameraStreamViewModel.DefaultRecordingAfterSeconds;
        private bool recordingGif = CameraStreamViewModel.DefaultRecordingGif;
        private bool recordingPng = CameraStreamViewModel.DefaultRecordingPng;
        private bool recordingMp4 = CameraStreamViewModel.DefaultRecordingMp4;
        private string? recordingOutputPath;

        public int Id
        {
            get => this.id;
            set
            {
                if (this.SetProperty(ref this.id, value))
                {
                    // Load persisted settings when ID is set
                    this.LoadSoundSettings();
                    this.LoadRecordingSettings();
                }
            }
        }

        private void LoadSoundSettings()
        {
            string volumeKey = string.Format(CameraStreamViewModel.SoundVolumeKeyFormat, this.Id);
            this.soundVolume = Preferences.Get(volumeKey, CameraStreamViewModel.DefaultSoundVolume);
            this.OnPropertyChanged(nameof(this.SoundVolume));

            string enabledKey = string.Format(CameraStreamViewModel.SoundEnabledKeyFormat, this.Id);
            this.soundEnabled = Preferences.Get(enabledKey, CameraStreamViewModel.DefaultSoundEnabled);
            this.OnPropertyChanged(nameof(this.SoundEnabled));

            string filePathKey = string.Format(CameraStreamViewModel.SoundFilePathKeyFormat, this.Id);
            this.soundFilePath = Preferences.Get(filePathKey, null);
            this.OnPropertyChanged(nameof(this.SoundFilePath));
            this.OnPropertyChanged(nameof(this.SoundFileName));
        }

        private void LoadRecordingSettings()
        {
            string enabledKey = string.Format(CameraStreamViewModel.RecordingEnabledKeyFormat, this.Id);
            this.recordingEnabled = Preferences.Get(enabledKey, CameraStreamViewModel.DefaultRecordingEnabled);
            this.OnPropertyChanged(nameof(this.RecordingEnabled));

            // RecordingBeforeSeconds and RecordingAfterSeconds are now fixed constants (5 and 10)
            // No need to load from preferences

            string gifKey = string.Format(CameraStreamViewModel.RecordingGifKeyFormat, this.Id);
            this.recordingGif = Preferences.Get(gifKey, CameraStreamViewModel.DefaultRecordingGif);
            this.OnPropertyChanged(nameof(this.RecordingGif));

            string pngKey = string.Format(CameraStreamViewModel.RecordingPngKeyFormat, this.Id);
            this.recordingPng = Preferences.Get(pngKey, CameraStreamViewModel.DefaultRecordingPng);
            this.OnPropertyChanged(nameof(this.RecordingPng));

            string mp4Key = string.Format(CameraStreamViewModel.RecordingMp4KeyFormat, this.Id);
            this.recordingMp4 = Preferences.Get(mp4Key, CameraStreamViewModel.DefaultRecordingMp4);
            this.OnPropertyChanged(nameof(this.RecordingMp4));

            string outputPathKey = string.Format(CameraStreamViewModel.RecordingOutputPathKeyFormat, this.Id);
            this.recordingOutputPath = Preferences.Get(outputPathKey, null);
            this.OnPropertyChanged(nameof(this.RecordingOutputPath));
        }

        public string CameraName
        {
            get => this.cameraName;
            set => this.SetProperty(ref this.cameraName, value);
        }

        public string Url
        {
            get => this.url;
            set => this.SetProperty(ref this.url, value);
        }

        public bool IsRunning
        {
            get => this.isRunning;
            set => this.SetProperty(ref this.isRunning, value);
        }

        public ImageSource? CurrentFrame
        {
            get => this.currentFrame;
            set => this.SetProperty(ref this.currentFrame, value);
        }

        public string Metrics
        {
            get => this.metrics;
            set => this.SetProperty(ref this.metrics, value);
        }

        public string MotionStatus
        {
            get => this.motionStatus;
            set => this.SetProperty(ref this.motionStatus, value);
        }

        public Color MotionColor
        {
            get => this.motionColor;
            set => this.SetProperty(ref this.motionColor, value);
        }

        public float LastRatio
        {
            get => this.lastRatio;
            set => this.SetProperty(ref this.lastRatio, value);
        }

        public double MotionThresholdPercent
        {
            get => this.motionThresholdPercent;
            set => this.SetProperty(ref this.motionThresholdPercent, value);
        }

        public double SoundVolume
        {
            get => this.soundVolume;
            set
            {
                if (this.SetProperty(ref this.soundVolume, value))
                {
                    // Persist volume setting
                    string key = string.Format(CameraStreamViewModel.SoundVolumeKeyFormat, this.Id);
                    Preferences.Set(key, value);
                }
            }
        }

        public bool SoundEnabled
        {
            get => this.soundEnabled;
            set
            {
                if (this.SetProperty(ref this.soundEnabled, value))
                {
                    // Persist enabled setting
                    string key = string.Format(CameraStreamViewModel.SoundEnabledKeyFormat, this.Id);
                    Preferences.Set(key, value);
                }
            }
        }

        public string? SoundFilePath
        {
            get => this.soundFilePath;
            set
            {
                if (this.SetProperty(ref this.soundFilePath, value))
                {
                    // Persist file path setting
                    string key = string.Format(CameraStreamViewModel.SoundFilePathKeyFormat, this.Id);
                    if (!string.IsNullOrEmpty(value))
                    {
                        Preferences.Set(key, value);
                    }
                    else
                    {
                        Preferences.Remove(key);
                    }
                    this.OnPropertyChanged(nameof(this.SoundFileName));
                }
            }
        }

        public string SoundFileName
        {
            get
            {
                if (string.IsNullOrEmpty(this.soundFilePath))
                {
                    return "No file selected";
                }
                return System.IO.Path.GetFileName(this.soundFilePath);
            }
        }

        public bool RecordingEnabled
        {
            get => this.recordingEnabled;
            set
            {
                if (this.SetProperty(ref this.recordingEnabled, value))
                {
                    string key = string.Format(CameraStreamViewModel.RecordingEnabledKeyFormat, this.Id);
                    Preferences.Set(key, value);
                }
            }
        }

        public int RecordingBeforeSeconds
        {
            get => 5;  // Fixed at 5 seconds
            set { }    // No-op setter for binding compatibility
        }

        public int RecordingAfterSeconds
        {
            get => 10;  // Fixed at 10 seconds
            set { }     // No-op setter for binding compatibility
        }

        public bool RecordingGif
        {
            get => this.recordingGif;
            set
            {
                if (this.SetProperty(ref this.recordingGif, value))
                {
                    string key = string.Format(CameraStreamViewModel.RecordingGifKeyFormat, this.Id);
                    Preferences.Set(key, value);
                }
            }
        }

        public bool RecordingPng
        {
            get => this.recordingPng;
            set
            {
                if (this.SetProperty(ref this.recordingPng, value))
                {
                    string key = string.Format(CameraStreamViewModel.RecordingPngKeyFormat, this.Id);
                    Preferences.Set(key, value);
                }
            }
        }

        public bool RecordingMp4
        {
            get => this.recordingMp4;
            set
            {
                if (this.SetProperty(ref this.recordingMp4, value))
                {
                    string key = string.Format(CameraStreamViewModel.RecordingMp4KeyFormat, this.Id);
                    Preferences.Set(key, value);
                }
            }
        }

        public string? RecordingOutputPath
        {
            get => this.recordingOutputPath;
            set
            {
                if (this.SetProperty(ref this.recordingOutputPath, value))
                {
                    string key = string.Format(CameraStreamViewModel.RecordingOutputPathKeyFormat, this.Id);
                    if (!string.IsNullOrEmpty(value))
                    {
                        Preferences.Set(key, value);
                    }
                    else
                    {
                        Preferences.Remove(key);
                    }
                }
            }
        }

        public ObservableCollection<string> DetectionLogs { get; } = new();

        public MjpegStreamer? Streamer { get; set; }
        
        // Frame buffer for recording
        public IPCameraViewer.Services.FrameBuffer? FrameBuffer { get; set; }
        
        // Recording state
        public bool IsRecording { get; set; }
        public DateTime? RecordingStartTime { get; set; }
        public List<IPCameraViewer.Services.FrameData> RecordingFrames { get; set; } = new();
        
        // Lock object for thread-safe access to RecordingFrames
        public object RecordingFramesLock { get; } = new object();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            bool result = true;
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                result = false;
            }
            else
            {
                field = value;
                this.OnPropertyChanged(propertyName);
            }
            return result;
        }
    }
}