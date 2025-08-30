using System.Text.RegularExpressions;
using DietMVP.Services;
using Supabase.Gotrue;

namespace DietMVP.Pages.Doctor;

public partial class SettingsPage : ContentPage
{
    public SettingsPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Supa.InitAsync();
        LblEmail.Text = Supa.Client.Auth.CurrentUser?.Email ?? "-";
    }

    // ---------- EMAIL ----------
    private async void OnUpdateEmail(object? sender, EventArgs e)
    {
        var email1 = (NewEmail.Text ?? "").Trim();
        var email2 = (NewEmail2.Text ?? "").Trim();
        var pwd = (PwdForEmail.Text ?? "").Trim();

        if (!IsValidEmail(email1)) { await Toast("Ge�erli bir e-posta gir."); return; }
        if (!string.Equals(email1, email2, StringComparison.OrdinalIgnoreCase)) { await Toast("E-posta tekrar� uyu�muyor."); return; }
        if (string.IsNullOrWhiteSpace(pwd)) { await Toast("Mevcut �ifreni gir."); return; }

        await WithBusy("E-posta g�ncelleniyor...", async () =>
        {
            await Supa.InitAsync();
            var current = Supa.Client.Auth.CurrentUser?.Email ?? "";
            if (string.IsNullOrWhiteSpace(current)) throw new Exception("Oturum bulunamad�.");

            // G�venlik: re-auth
            await Supa.Client.Auth.SignIn(current, pwd);

            // G�ncelle
            await Supa.Client.Auth.Update(new UserAttributes { Email = email1 });

            LblEmail.Text = email1; // do�rulama beklese de kullan�c� g�rs�n
            NewEmail.Text = NewEmail2.Text = PwdForEmail.Text = string.Empty;

            await Toast("Do�rulama e-postas� g�nderildi.");
        });
    }

    // ---------- PASSWORD ----------
    private async void OnUpdatePassword(object? sender, EventArgs e)
    {
        var oldPwd = (OldPwd.Text ?? "").Trim();
        var newPwd = (NewPwd.Text ?? "").Trim();
        var newPwd2 = (NewPwd2.Text ?? "").Trim();

        if (string.IsNullOrWhiteSpace(oldPwd)) { await Toast("Mevcut �ifreni gir."); return; }
        if (newPwd.Length < 8) { await Toast("Yeni �ifre en az 8 karakter olmal�."); return; }
        if (newPwd != newPwd2) { await Toast("Yeni �ifre tekrar� uyu�muyor."); return; }
        if (newPwd == oldPwd) { await Toast("Yeni �ifre mevcut �ifreyle ayn� olamaz."); return; }

        await WithBusy("�ifre g�ncelleniyor...", async () =>
        {
            await Supa.InitAsync();
            var email = Supa.Client.Auth.CurrentUser?.Email ?? "";
            if (string.IsNullOrWhiteSpace(email)) throw new Exception("Oturum bulunamad�.");

            // re-auth
            await Supa.Client.Auth.SignIn(email, oldPwd);

            // G�ncelle
            await Supa.Client.Auth.Update(new UserAttributes { Password = newPwd });

            OldPwd.Text = NewPwd.Text = NewPwd2.Text = string.Empty;
            await Toast("�ifre g�ncellendi.");
        });
    }

    // ---------- SIGN OUT ----------
    private async void OnSignOut(object? sender, EventArgs e)
    {
        await WithBusy("��k�� yap�l�yor...", async () =>
        {
            await Supa.InitAsync();
            await Supa.Client.Auth.SignOut();
        });

        await Toast("��k�� yap�ld�.");
        // Gerekirse: await Shell.Current.GoToAsync("//LoginPage");
    }

    // ---------- Show/Hide toggles ----------
    private void TogglePwdForEmail(object? sender, CheckedChangedEventArgs e)
     => PwdForEmail.IsPassword = !e.Value;

    private void TogglePwdOld(object? sender, CheckedChangedEventArgs e)
        => OldPwd.IsPassword = !e.Value;

    private void TogglePwdNew(object? sender, CheckedChangedEventArgs e)
        => NewPwd.IsPassword = !e.Value;

    private void TogglePwdNew2(object? sender, CheckedChangedEventArgs e)
        => NewPwd2.IsPassword = !e.Value;

    // ---------- Helpers ----------
    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        return Regex.IsMatch(email,
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private async Task WithBusy(string text, Func<Task> work)
    {
        Msg.IsVisible = false;

        BusyText.Text = text;
        BusyOverlay.IsVisible = true;
        await Task.WhenAll(
            BusyCard.FadeTo(1, 120),
            BusyCard.TranslateTo(0, 0, 120, Easing.CubicOut)
        );

        try { await work(); }
        catch (Exception ex)
        {
            Msg.Text = ex.Message;
            Msg.IsVisible = true;
            await Toast(ex.Message);
        }
        finally
        {
            await Task.WhenAll(
                BusyCard.FadeTo(0, 120),
                BusyCard.TranslateTo(0, 40, 120, Easing.CubicIn)
            );
            BusyOverlay.IsVisible = false;
        }
    }

    private async Task Toast(string text, int ms = 1400)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        ToastText.Text = text;
        ToastHost.Opacity = 0;
        ToastHost.IsVisible = true;
        await ToastHost.FadeTo(1, 120);
        await Task.Delay(ms);
        await ToastHost.FadeTo(0, 120);
        ToastHost.IsVisible = false;
    }
}
