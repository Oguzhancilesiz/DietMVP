using DietMVP.Pages;
using DietMVP.Services;

namespace DietMVP
{
    public partial class PatientShell : Shell
    {
        public PatientShell()
        {
            InitializeComponent();
        }

        private async void OnLogout(object? sender, EventArgs e)
        {
            var auth = new AuthService();
            await auth.SignOutAsync();
            AppSession.CurrentProfile = null;
            Application.Current!.MainPage = new NavigationPage(new LoginPage());
        }
    }
}
