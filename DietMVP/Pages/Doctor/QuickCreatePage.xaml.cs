using DietMVP.Services;
using System.Text.RegularExpressions;

namespace DietMVP.Pages.Doctor;

public partial class QuickCreatePage : ContentPage
{
    private readonly PatientService _ps = new();
    private readonly DietService _diet = new();

    public QuickCreatePage()
    {
        InitializeComponent();

        StartDate.Date = DateTime.Today;

        // Varsayılan saatler
        BrStart.Time = new TimeSpan(8, 0, 0); BrEnd.Time = new TimeSpan(9, 0, 0);
        S1Start.Time = new TimeSpan(11, 0, 0); S1End.Time = new TimeSpan(11, 30, 0);
        LuStart.Time = new TimeSpan(13, 0, 0); LuEnd.Time = new TimeSpan(14, 0, 0);
        S2Start.Time = new TimeSpan(16, 0, 0); S2End.Time = new TimeSpan(16, 30, 0);
        DiStart.Time = new TimeSpan(19, 0, 0); DiEnd.Time = new TimeSpan(20, 0, 0);

        ChkBr.IsChecked = ChkLu.IsChecked = ChkDi.IsChecked = true;
    }

    // ---------- UI Helpers ----------
    private async Task ShowToast(string text, int ms = 1500)
    {
        ToastText.Text = text;
        ToastHost.Opacity = 0;
        ToastHost.IsVisible = true;
        await ToastHost.FadeTo(1, 150);
        await Task.Delay(ms);
        await ToastHost.FadeTo(0, 150);
        ToastHost.IsVisible = false;
    }

    private async Task WithBusy(string text, Func<Task> action)
    {
        await ShowBusy(text);
        try { await action(); }
        catch (Exception ex)
        {
            var msg = Flatten(ex);
            Msg.Text = msg;
            Msg.IsVisible = true;
            await ShowToast(msg, 1800);
        }
        finally { await HideBusy(); }
    }

    private async Task ShowBusy(string text)
    {
        Msg.IsVisible = false;
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

    private static string Flatten(Exception ex)
    {
        var s = (ex.Message ?? "").Trim();
        while (ex.InnerException != null && (ex.InnerException.Message?.Length ?? 0) < 200)
        {
            ex = ex.InnerException;
            s = (ex.Message ?? "").Trim();
        }
        return string.IsNullOrWhiteSpace(s) ? "Beklenmeyen bir hata oluştu." : s!;
    }

    // Basit e-posta doğrulama
    private static bool IsValidEmail(string email)
        => !string.IsNullOrWhiteSpace(email) &&
           Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);

    // Zaman aralığı doğrulama
    private bool ValidateTimes(string slot, bool on, TimeSpan s, TimeSpan e, List<string> errors)
    {
        if (!on) return true;
        if (s >= e)
        {
            errors.Add($"{slot}: başlangıç bitișten küçük olmalı.");
            return false;
        }
        return true;
    }

    // ---------- CREATE ----------
    private async void OnCreate(object sender, EventArgs e)
    {
        Msg.IsVisible = false;

        var full = FullNameEntry.Text?.Trim() ?? "";
        var mail = EmailEntry.Text?.Trim() ?? "";
        var pw = PwEntry.Text ?? "";

        if (string.IsNullOrWhiteSpace(full)) { await ShowToast("Ad soyad zorunlu."); return; }
        if (!IsValidEmail(mail)) { await ShowToast("Geçerli bir e-posta gir."); return; }
        if (pw.Length < 8) { await ShowToast("Şifre en az 8 karakter olmalı."); return; }

        var water = int.TryParse(WaterEntry.Text, out var ml) ? ml : 2000;
        water = Math.Clamp(water, 500, 6000);

        var days = int.TryParse(DaysEntry.Text, out var d) ? d : 30;
        days = Math.Clamp(days, 1, 365);

        var start = DateOnly.FromDateTime(StartDate.Date);

        // En az bir öğün seçilmiş mi?
        if (!ChkBr.IsChecked && !ChkS1.IsChecked && !ChkLu.IsChecked && !ChkS2.IsChecked && !ChkDi.IsChecked)
        {
            await ShowToast("En az bir öğün seçmelisin.");
            return;
        }

        // Saat kontrolleri
        var errs = new List<string>();
        ValidateTimes("Kahvaltı", ChkBr.IsChecked, BrStart.Time, BrEnd.Time, errs);
        ValidateTimes("Ara Öğün 1", ChkS1.IsChecked, S1Start.Time, S1End.Time, errs);
        ValidateTimes("Öğle", ChkLu.IsChecked, LuStart.Time, LuEnd.Time, errs);
        ValidateTimes("Ara Öğün 2", ChkS2.IsChecked, S2Start.Time, S2End.Time, errs);
        ValidateTimes("Akşam", ChkDi.IsChecked, DiStart.Time, DiEnd.Time, errs);

        if (errs.Count > 0)
        {
            await ShowToast(string.Join("\n", errs), 2200);
            return;
        }

        await WithBusy("Program oluşturuluyor...", async () =>
        {
            // 1) Hasta oluştur/bağla
            var patientId = await _ps.CreatePatientAsync(mail, pw, full, water);

            // 2) Program başlığı
            var prog = await _diet.CreateProgramHeaderAsync(patientId, start, days, water);

            // 3) Seçili öğünler
            var selected = new List<string>();
            var times = new Dictionary<string, (TimeOnly s, TimeOnly e)>();

            void addIf(bool on, string slot, TimeSpan s, TimeSpan e)
            {
                if (!on) return;
                selected.Add(slot);
                times[slot] = (TimeOnly.FromTimeSpan(s), TimeOnly.FromTimeSpan(e));
            }

            addIf(ChkBr.IsChecked, "Breakfast", BrStart.Time, BrEnd.Time);
            addIf(ChkS1.IsChecked, "Snack1", S1Start.Time, S1End.Time);
            addIf(ChkLu.IsChecked, "Lunch", LuStart.Time, LuEnd.Time);
            addIf(ChkS2.IsChecked, "Snack2", S2Start.Time, S2End.Time);
            addIf(ChkDi.IsChecked, "Dinner", DiStart.Time, DiEnd.Time);

            // 4) Gün + öğün tohumla
            await _diet.SeedDaysAndMealsAsync(prog.Id, selected, times);

            // 5) Bildir ve detay sayfasına geç
            await ShowToast("Program oluşturuldu.");
            await Navigation.PushAsync(new ProgramDetailPage(prog));
        });
    }
}
