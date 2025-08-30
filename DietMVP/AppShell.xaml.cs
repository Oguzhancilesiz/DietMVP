using DietMVP.Pages;
using DietMVP.Services;

namespace DietMVP
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
        }

        private async void OnLogout(object? sender, EventArgs e)
        {
            var auth = new AuthService();
            await auth.SignOutAsync();

            Application.Current!.MainPage = new NavigationPage(new LoginPage());
        }
    }
}
