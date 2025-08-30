using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Plugin.LocalNotification;

#if ANDROID
using Android.Views;
using Android.Graphics;
using AndroidX.Core.View;
#endif

namespace DietMVP;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkitCore()    // Core bu
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        builder.ConfigureLifecycleEvents(events =>
        {
#if ANDROID
            events.AddAndroid(android => android.OnCreate((activity, bundle) =>
            {
                // Status bar'ı tamamen gizle
                WindowCompat.SetDecorFitsSystemWindows(activity.Window, true);
            }));
#endif
        });
#if DEBUG
        builder.Logging.AddDebug();
#endif
        builder.UseLocalNotification();

        return builder.Build();
    }
}