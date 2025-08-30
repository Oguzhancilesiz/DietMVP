using System.Threading;
using DietMVP.Pages;
using DietMVP.Services;
using Plugin.LocalNotification;
using Microsoft.Maui.ApplicationModel; // MainThread

namespace DietMVP
{
    public partial class App : Application
    {
        private static readonly SemaphoreSlim _rootSwap = new(1, 1);

        public App()
        {
            InitializeComponent();

            // Şık loader ilk ekranda
            MainPage = new Pages.LoadingPage();

            _ = StartupAsync();
        }

        protected override async void OnStart()
        {
            base.OnStart();
            try
            {
                await LocalNotificationCenter.Current.RequestNotificationPermission();
                NotificationService.Init();
            }
            catch { /* sessiz geç */ }
        }

        private async Task StartupAsync()
        {
            try
            {
                var auth = new AuthService();

                var resumed = await auth.TryResumeSessionAsync();
                if (!resumed)
                {
                    await SetRootAsync(new NavigationPage(new LoginPage()));
                    return;
                }

                var profile = await auth.GetCurrentProfileAsync();
                if (profile == null)
                {
                    await SetRootAsync(new NavigationPage(new LoginPage()));
                    return;
                }

                await Bootstrapper.LaunchForAsync(profile);
            }
            catch
            {
                await SetRootAsync(new NavigationPage(new LoginPage()));
            }
        }

        public static async Task SetRootAsync(Page root)
        {
            await _rootSwap.WaitAsync();
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Current!.MainPage = root;
                });
            }
            finally
            {
                _rootSwap.Release();
            }
        }
    }
}
