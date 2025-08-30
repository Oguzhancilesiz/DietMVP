using DietMVP.Services;

namespace DietMVP.Pages
{
    public partial class LoginPage : ContentPage
    {
        private readonly AuthService _auth = new();
        private bool _busy;

        public LoginPage()
        {
            InitializeComponent();
            Title = "Giri�";
        }

        private async void OnLogin(object? sender, EventArgs e)
        {
            if (_busy) return;

            Status.IsVisible = false;
            var email = EmailEntry.Text?.Trim() ?? "";
            var pass = PwEntry.Text ?? "";

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
            {
                ShowStatus("E-posta ve �ifre gerekli.", true);
                return;
            }

            await WithBusy("Giri� yap�l�yor...", async () =>
            {
                var ok = await _auth.SignInAsync(email, pass);
                if (!ok)
                {
                    ShowStatus("Giri� ba�ar�s�z. Bilgileri kontrol et.", true);
                    return;
                }

                var profile = await _auth.GetCurrentProfileAsync();
                if (profile == null)
                {
                    ShowStatus("Profil bulunamad�.", true);
                    return;
                }

                await Bootstrapper.LaunchForAsync(profile);
            });
        }

        private void OnTogglePwd(object? sender, CheckedChangedEventArgs e)
        {
            PwEntry.IsPassword = !e.Value; // i�aretli ise g�ster
        }

        private void ShowStatus(string text, bool error)
        {
            Status.Text = text;
            Status.TextColor = error ? Colors.IndianRed : Colors.ForestGreen;
            Status.IsVisible = true;
        }

        private async Task WithBusy(string text, Func<Task> work)
        {
            try
            {
                _busy = true;
                BusyText.Text = text;
                BusyOverlay.IsVisible = true;

                await Task.WhenAll(
                    BusyCard.FadeTo(1, 140),
                    BusyCard.TranslateTo(0, 0, 140, Easing.CubicOut)
                );

                await work();
            }
            catch (Exception ex)
            {
                ShowStatus(ex.Message, true);
            }
            finally
            {
                await Task.WhenAll(
                    BusyCard.FadeTo(0, 140),
                    BusyCard.TranslateTo(0, 40, 140, Easing.CubicIn)
                );
                BusyOverlay.IsVisible = false;
                _busy = false;
            }
        }
    }
}
