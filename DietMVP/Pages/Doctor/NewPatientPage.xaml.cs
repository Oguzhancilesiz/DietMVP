using System.Text.RegularExpressions;
using DietMVP.Services;

namespace DietMVP.Pages.Doctor;

public partial class NewPatientPage : ContentPage
{
    private readonly PatientService _ps = new();

    public NewPatientPage()
    {
        InitializeComponent();
    }

    // -------- Actions
    private async void OnCreate(object sender, EventArgs e)
    {
        Msg.IsVisible = false;

        var full = (FullNameEntry.Text ?? string.Empty).Trim();
        var mail = (EmailEntry.Text ?? string.Empty).Trim();
        var pw = PwEntry.Text ?? string.Empty;
        var water = int.TryParse(WaterEntry.Text, out var ml) ? ml : 0;

        var error = Validate(full, mail, pw, water);
        if (error is not null)
        {
            Msg.Text = error;
            Msg.IsVisible = true;
            await ShowToast(error);
            return;
        }

        try
        {
            await WithBusy("Hasta oluþturuluyor...", async () =>
            {
                await _ps.CreatePatientAsync(mail, pw, full, water);
            });

            await ShowToast("Hasta oluþturuldu.");
            OnReset(null!, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Msg.Text = ex.Message;
            Msg.IsVisible = true;
            await ShowToast($"Hata: {ex.Message}");
        }
    }

    private void OnReset(object? sender, EventArgs e)
    {
        FullNameEntry.Text = "";
        EmailEntry.Text = "";
        PwEntry.Text = "";
        WaterEntry.Text = "2000";
        ShowPw.IsChecked = false;
        Msg.IsVisible = false;
    }

    private void OnShowPwChanged(object? sender, CheckedChangedEventArgs e)
        => PwEntry.IsPassword = !e.Value;

    // -------- Validation
    private static string? Validate(string full, string mail, string pw, int water)
    {
        if (string.IsNullOrWhiteSpace(full)) return "Ad soyad gerekli.";
        if (string.IsNullOrWhiteSpace(mail)) return "E-posta gerekli.";

        var okMail = Regex.IsMatch(mail, @"^\S+@\S+\.\S+$");
        if (!okMail) return "E-posta formatý geçersiz.";

        if (string.IsNullOrWhiteSpace(pw) || pw.Length < 6)
            return "Geçici þifre en az 6 karakter olmalý.";

        if (water <= 0 || water > 10000)
            return "Su hedefi 1–10000 ml aralýðýnda olmalý.";

        return null;
    }

    // -------- Busy + Toast (animasyonlu)
    private async Task WithBusy(string text, Func<Task> work)
    {
        await ShowBusy(text);
        try { await work(); }
        finally { await HideBusy(); }
    }

    private async Task ShowBusy(string text)
    {
        BusyText.Text = text;
        BusyOverlay.IsVisible = true;
        BusyOverlay.InputTransparent = false;
        BusyCard.Opacity = 0;
        BusyCard.Scale = 0.96;
        await Task.WhenAll(
            BusyCard.FadeTo(1, 140, Easing.CubicOut),
            BusyCard.ScaleTo(1.0, 140, Easing.CubicOut)
        );
    }

    private async Task HideBusy()
    {
        await Task.WhenAll(
            BusyCard.FadeTo(0, 120, Easing.CubicIn),
            BusyCard.ScaleTo(0.96, 120, Easing.CubicIn)
        );
        BusyOverlay.IsVisible = false;
        BusyOverlay.InputTransparent = true;
    }

    private async Task ShowToast(string text, int ms = 1500)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var card = (ToastHost.Children[1] as Frame)!; // Grid.Row=1

        ToastHost.IsVisible = true;
        card.Opacity = 0;
        await card.FadeTo(1, 150);
        (ToastHost.FindByName<Label>("ToastText"))!.Text = text;
        await Task.Delay(ms);
        await card.FadeTo(0, 150);
        ToastHost.IsVisible = false;
    }
}
