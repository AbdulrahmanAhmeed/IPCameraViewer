using Microsoft.Extensions.Logging;
using IPCameraViewer.Services;

namespace IPCameraViewer
{
    public static class MauiProgram
    {
        private const string OpenSansRegularFont = "OpenSans-Regular.ttf";
        private const string OpenSansRegularFontName = "OpenSansRegular";
        private const string OpenSansSemiboldFont = "OpenSans-Semibold.ttf";
        private const string OpenSansSemiboldFontName = "OpenSansSemibold";

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont(MauiProgram.OpenSansRegularFont, MauiProgram.OpenSansRegularFontName);
                    fonts.AddFont(MauiProgram.OpenSansSemiboldFont, MauiProgram.OpenSansSemiboldFontName);
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

		builder.Services.AddSingleton<HttpClient>();
		builder.Services.AddSingleton<MjpegStreamer>();

#if WINDOWS
            builder.Services.AddSingleton<IAudioService, Platforms.Windows.AudioService>();
#else
            builder.Services.AddSingleton<IAudioService, NoOpAudioService>();
#endif

            return builder.Build();
        }
    }
}
