using System.Text.RegularExpressions;
using DietMVP.Models;
using DietMVP.Services;
using Plugin.LocalNotification;
using Supabase.Gotrue;
using static Supabase.Postgrest.Constants;

namespace DietMVP.Pages.Patient;

public partial class SettingsPage : ContentPage
{
    private Profile? _profile;

    public SettingsPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Supa.InitAsync();

        // Hesap e-postası
        LblEmail.Text = Supa.Client.Auth.CurrentUser?.Email ?? "-";

        // Profil yükle/oluştur
        await LoadOrCreateProfileAsync();
        BindProfileToUi();
    }

    // ---------------- PROFILE ----------------
    private async Task LoadOrCreateProfileAsync()
    {
        var uidStr = Supa.Client.Auth.CurrentUser?.Id ?? "";
        if (!Guid.TryParse(uidStr, out var uid))
            throw new Exception("Auth bulunamadı.");

        var resp = await Supa.Client.From<Profile>()
            .Filter("id", Operator.Equals, uid.ToString())
            .Get();

        _profile = resp.Models.FirstOrDefault();

        if (_profile == null)
        {
            // yoksa oluştur (role: patient)
            _profile = new Profile
            {
                Id = uid,
                Role = "patient",
                FullName = "",
                Phone = null,
                DailyWaterTargetMl = 2000
            };
            await Supa.Client.From<Profile>().Upsert(_profile);
        }
        else if (string.IsNullOrWhiteSpace(_profile.Role))
        {
            _profile.Role = "patient";
            await Supa.Client.From<Profile>().Upsert(_profile);
        }
    }

    private void BindProfileToUi()
    {
        if (_profile == null) return;
        TxtFullName.Text = _profile.FullName ?? "";
        TxtPhone.Text = _profile.Phone ?? "";
        StepWater.Value = Math.Clamp(_profile.DailyWaterTargetMl, 1000, 5000);
        LblWater.Text = $"{(int)StepWater.Value} ml";
        WaterBar.Progress = Math.Min(1.0, StepWater.Value / 3000d); // görsel amaçlı
    }

    private void OnWaterChanged(object? s, ValueChangedEventArgs e)
    {
        LblWater.Text = $"{(int)StepWater.Value} ml";
        WaterBar.Progress = Math.Min(1.0, StepWater.Value / 3000d);
    }

    private async void OnSaveProfile(object? sender, EventArgs e)
    {
        if (_profile == null) return;

        _profile.FullName = (TxtFullName.Text ?? "").Trim();
        _profile.Phone = string.IsNullOrWhiteSpace(TxtPhone.Text) ? null : TxtPhone.Text!.Trim();
        _profile.DailyWaterTargetMl = (int)StepWater.Value;

        await WithBusy("Profil kaydediliyor...", async () =>
        {
            await Supa.Client.From<Profile>().Upsert(_profile);
            await Toast("Profil güncellendi.");
        });
    }

    // ---------------- EMAIL ----------------
    private async void OnUpdateEmail(object? sender, EventArgs e)
    {
        var email1 = (NewEmail.Text ?? "").Trim();
        var email2 = (NewEmail2.Text ?? "").Trim();
        var pwd = (PwdForEmail.Text ?? "").Trim();

        if (!IsValidEmail(email1)) { await Toast("Geçerli bir e-posta gir."); return; }
        if (!string.Equals(email1, email2, StringComparison.OrdinalIgnoreCase)) { await Toast("E-posta tekrarı uyuşmuyor."); return; }
        if (string.IsNullOrWhiteSpace(pwd)) { await Toast("Mevcut şifreni gir."); return; }

        await WithBusy("E-posta güncelleniyor...", async () =>
        {
            var current = Supa.Client.Auth.CurrentUser?.Email ?? "";
            if (string.IsNullOrWhiteSpace(current)) throw new Exception("Oturum bulunamadı.");

            // re-auth (SDK sürümüne göre SignInWithPassword olabilir)
            await Supa.Client.Auth.SignIn(current, pwd);

            await Supa.Client.Auth.Update(new UserAttributes { Email = email1 });

            LblEmail.Text = email1;
            NewEmail.Text = NewEmail2.Text = PwdForEmail.Text = string.Empty;
            await Toast("Doğrulama e-postası gönderildi.");
        });
    }

    // ---------------- PASSWORD ----------------
    private async void OnUpdatePassword(object? sender, EventArgs e)
    {
        var oldPwd = (OldPwd.Text ?? "").Trim();
        var newPwd = (NewPwd.Text ?? "").Trim();
        var newPwd2 = (NewPwd2.Text ?? "").Trim();

        if (string.IsNullOrWhiteSpace(oldPwd)) { await Toast("Mevcut şifreyi gir."); return; }
        if (newPwd.Length < 8) { await Toast("Yeni şifre en az 8 karakter olmalı."); return; }
        if (newPwd != newPwd2) { await Toast("Yeni şifre tekrarı uyuşmuyor."); return; }
        if (newPwd == oldPwd) { await Toast("Yeni şifre mevcutla aynı olamaz."); return; }

        await WithBusy("Şifre güncelleniyor...", async () =>
        {
            var email = Supa.Client.Auth.CurrentUser?.Email ?? "";
            if (string.IsNullOrWhiteSpace(email)) throw new Exception("Oturum bulunamadı.");

            // re-auth
            await Supa.Client.Auth.SignIn(email, oldPwd);

            await Supa.Client.Auth.Update(new UserAttributes { Password = newPwd });

            OldPwd.Text = NewPwd.Text = NewPwd2.Text = string.Empty;
            await Toast("Şifre güncellendi.");
        });
    }

    // ---------------- NOTIFICATIONS ----------------
    private async void OnAskPermission(object sender, EventArgs e)
    {
        try { _ = LocalNotificationCenter.Current.RequestNotificationPermission(); }
        catch (Exception ex) { await DisplayAlert("Hata", ex.Message, "Tamam"); return; }
        await Toast("Bildirim izni isteği gönderildi.");
    }

    private async void OnTestNotification(object sender, EventArgs e)
    {
        var when = DateTime.Now.AddSeconds(5);
        await LocalNotificationCenter.Current.Show(new NotificationRequest
        {
            NotificationId = 990001,
            Title = "Test Bildirim",
            Description = "5 sn sonra gelen test bildirimi.",
            Schedule = new NotificationRequestSchedule { NotifyTime = when, RepeatType = NotificationRepeat.No }
        });
        await Toast("Test bildirimi planlandı.");
    }

    private void OnClearAll(object sender, EventArgs e)
    { try { LocalNotificationCenter.Current.CancelAll(); } catch { } }

    // ---------------- SIGN OUT ----------------
    private async void OnSignOut(object? sender, EventArgs e)
    {
        var ok = await DisplayAlert("Çıkış", "Hesaptan çıkmak istiyor musun?", "Evet", "Vazgeç");
        if (!ok) return;

        await WithBusy("Çıkış yapılıyor...", async () =>
        {
            await Supa.Client.Auth.SignOut();
        });

        await Toast("Çıkış yapıldı.");
        // Gerekirse giriş sayfasına yönlendir:
        // await Shell.Current.GoToAsync("//LoginPage");
    }

    // ---------------- HELPERS ----------------
    private static bool IsValidEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) &&
        Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);

    private async Task WithBusy(string text, Func<Task> work)
    {
        Msg.IsVisible = false;
        BusyText.Text = text;
        BusyOverlay.IsVisible = true;
        try { await work(); }
        catch (Exception ex)
        {
            Msg.Text = ex.Message;
            Msg.IsVisible = true;
            await Toast(ex.Message);
        }
        finally { BusyOverlay.IsVisible = false; }
    }

    private async Task Toast(string text, int ms = 1400)
    {
        ToastText.Text = text;
        ToastHost.Opacity = 0;
        ToastHost.IsVisible = true;
        await ToastHost.FadeTo(1, 120);
        await Task.Delay(ms);
        await ToastHost.FadeTo(0, 120);
        ToastHost.IsVisible = false;
    }
}
