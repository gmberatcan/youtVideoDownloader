using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using youtVideoDownloader.Services;

namespace youtVideoDownloader
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<IYoutubeDownloadService, YoutubeDownloadService>();
#if ANDROID
            builder.Services.AddSingleton<INotificationService, youtVideoDownloader.Platforms.Android.NotificationService>();
#else
            builder.Services.AddSingleton<INotificationService, DummyNotificationService>();
#endif
            builder.Services.AddTransient<ViewModels.MainViewModel>();
            builder.Services.AddTransient<MainPage>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
