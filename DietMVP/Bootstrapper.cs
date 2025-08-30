using DietMVP.Models;
using DietMVP.Pages;
using DietMVP.Pages.Doctor;
using DietMVP.Pages.Patient;
using static Supabase.Postgrest.Constants;

namespace DietMVP
{
    public static class Bootstrapper
    {
        public static async Task LaunchForAsync(Profile profile)
        {
            AppSession.CurrentProfile = profile;

            Page root = string.Equals(profile.Role, Roles.Doctor, StringComparison.OrdinalIgnoreCase)
                ? new AppShell()      // doktor shell
                : new PatientShell(); // hasta shell

            await App.SetRootAsync(root);
        }

        public static async Task LaunchForAsync(Guid userId)
        {
            await Supa.InitAsync();

            var resp = await Supa.Client.From<Profile>()
                .Filter("id", Operator.Equals, userId.ToString())
                .Get();

            var profile = resp.Models.FirstOrDefault();

            if (profile is null)
                await App.SetRootAsync(new NavigationPage(new LoginPage()));
            else
                await LaunchForAsync(profile);
        }
    }
}
