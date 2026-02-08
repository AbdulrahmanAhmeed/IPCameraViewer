using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using IPCameraViewer.Services;

namespace IPCameraViewer
{
    public partial class SettingsPage : ContentPage
    {
        private const string SoundFilePathKey = "MotionDetectionSoundFilePath";
        private const string SoundEnabledKey = "MotionDetectionSoundEnabled";
        private const string NoFileSelectedText = "No file selected";
        private const string ErrorTitle = "Error";
        private const string OkButtonText = "OK";
        private const string SelectWavFileTitle = "Select a WAV file";
        private const string WavFileExtension = ".wav";
        private const string FailedToSelectFileFormat = "Failed to select file: {0}";
        private const string NoValidSoundFileMessage = "No valid sound file selected.";
        private const string SuccessTitle = "Success";
        private const string SoundTestSuccessMessage = "Sound test triggered. You should hear the sound now.";
        private const string AudioServiceNotAvailableMessage = "Audio service is not available.";
        private const string CannotAccessAudioServiceMessage = "Cannot access audio service.";
        private const string FailedToPlaySoundFormat = "Failed to play sound: {0}";
        private const string EmptyString = "";

        private string? selectedFilePath;

        public SettingsPage()
        {
            InitializeComponent();
            this.LoadSettings();
        }

        private void LoadSettings()
        {
            // Load sound file path
            this.selectedFilePath = Preferences.Get(SettingsPage.SoundFilePathKey, SettingsPage.EmptyString);
            if (!string.IsNullOrEmpty(this.selectedFilePath) && File.Exists(this.selectedFilePath))
            {
                this.SelectedFileLabel.Text = Path.GetFileName(this.selectedFilePath);
                this.SelectedFileLabel.TextColor = Colors.Black;
                this.TestSoundButton.IsEnabled = true;
            }
            else
            {
                this.SelectedFileLabel.Text = SettingsPage.NoFileSelectedText;
                this.SelectedFileLabel.TextColor = Colors.Gray;
                this.selectedFilePath = null;
                this.TestSoundButton.IsEnabled = false;
            }

            // Load sound enabled state
            this.EnableSoundSwitch.IsToggled = Preferences.Get(SettingsPage.SoundEnabledKey, true);
        }

        private async void OnSelectFileClicked(object sender, EventArgs e)
        {
            try
            {
                var customFileType = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { SettingsPage.WavFileExtension } }
                    });

                var options = new PickOptions
                {
                    PickerTitle = SettingsPage.SelectWavFileTitle,
                    FileTypes = customFileType
                };

                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    this.selectedFilePath = result.FullPath;
                    this.SelectedFileLabel.Text = Path.GetFileName(this.selectedFilePath);
                    this.SelectedFileLabel.TextColor = Colors.Black;
                    this.TestSoundButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                await this.DisplayAlert(SettingsPage.ErrorTitle, string.Format(SettingsPage.FailedToSelectFileFormat, ex.Message), SettingsPage.OkButtonText);
            }
        }

        private void OnClearFileClicked(object sender, EventArgs e)
        {
            this.selectedFilePath = null;
            this.SelectedFileLabel.Text = SettingsPage.NoFileSelectedText;
            this.SelectedFileLabel.TextColor = Colors.Gray;
            this.TestSoundButton.IsEnabled = false;
        }

        private void OnTestSoundClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(this.selectedFilePath) && File.Exists(this.selectedFilePath))
            {
                try
                {
                    var app = Application.Current;
                    if (app?.Handler?.MauiContext?.Services != null)
                    {
                        var audioService = app.Handler.MauiContext.Services.GetService<IAudioService>();
                        if (audioService != null)
                        {
                            audioService.PlaySound(this.selectedFilePath);
                            this.DisplayAlert(SettingsPage.SuccessTitle, SettingsPage.SoundTestSuccessMessage, SettingsPage.OkButtonText);
                        }
                        else
                        {
                            this.DisplayAlert(SettingsPage.ErrorTitle, SettingsPage.AudioServiceNotAvailableMessage, SettingsPage.OkButtonText);
                        }
                    }
                    else
                    {
                        this.DisplayAlert(SettingsPage.ErrorTitle, SettingsPage.CannotAccessAudioServiceMessage, SettingsPage.OkButtonText);
                    }
                }
                catch (Exception ex)
                {
                    this.DisplayAlert(SettingsPage.ErrorTitle, string.Format(SettingsPage.FailedToPlaySoundFormat, ex.Message), SettingsPage.OkButtonText);
                }
            }
            else
            {
                this.DisplayAlert(SettingsPage.ErrorTitle, SettingsPage.NoValidSoundFileMessage, SettingsPage.OkButtonText);
            }
        }

        private void OnEnableSoundToggled(object sender, ToggledEventArgs e)
        {
            // Settings are saved when Save is clicked
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            // Save sound file path
            if (!string.IsNullOrEmpty(this.selectedFilePath) && File.Exists(this.selectedFilePath))
            {
                Preferences.Set(SettingsPage.SoundFilePathKey, this.selectedFilePath);
            }
            else
            {
                Preferences.Remove(SettingsPage.SoundFilePathKey);
            }

            // Save sound enabled state
            Preferences.Set(SettingsPage.SoundEnabledKey, this.EnableSoundSwitch.IsToggled);

            // Close the page
            await Navigation.PopModalAsync();
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}

