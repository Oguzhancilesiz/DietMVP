using DietMVP.Models;
using DietMVP.Services;

namespace DietMVP.Pages.Doctor;

public partial class ProgramWizardPage : ContentPage
{
    private readonly Profile _patient;
    private readonly DietService _diet = new();

    public ProgramWizardPage(Profile patient)
    {
        InitializeComponent();
        _patient = patient;

        PatientName.Text = $"Hasta: {_patient.FullName}";
        StartDate.Date = DateTime.Today;

        // Varsayılan saatler
        BrStart.Time = new TimeSpan(8, 0, 0); BrEnd.Time = new TimeSpan(9, 0, 0);
        S1Start.Time = new TimeSpan(11, 0, 0); S1End.Time = new TimeSpan(11, 30, 0);
        LuStart.Time = new TimeSpan(13, 0, 0); LuEnd.Time = new TimeSpan(14, 0, 0);
        S2Start.Time = new TimeSpan(16, 0, 0); S2End.Time = new TimeSpan(16, 30, 0);
        DiStart.Time = new TimeSpan(19, 0, 0); DiEnd.Time = new TimeSpan(20, 0, 0);
    }

    // ---------- UI helpers ----------
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

    private static bool ValidateRange(bool enabled, TimeSpan s, TimeSpan e, string label, List<string> errs)
    {
        if (!enabled) return true;
        if (s >= e)
        {
            errs.Add($"{label}: başlangıç bitișten küçük olmalı.");
            return false;
        }
        return true;
    }

    // ---------- Create ----------
    private async void OnCreate(object sender, EventArgs e)
    {
        Msg.IsVisible = false;

        var days = int.TryParse(DaysEntry.Text, out var d) ? d : 30;
        days = Math.Clamp(days, 1, 365);
        var start = DateOnly.FromDateTime(StartDate.Date);

        // En az bir slot seçili mi?
        if (!ChkBr.IsChecked && !ChkS1.IsChecked && !ChkLu.IsChecked && !ChkS2.IsChecked && !ChkDi.IsChecked)
        {
            await ShowToast("En az bir öğün seçmelisin.");
            return;
        }

        // Saat doğrulamaları
        var errs = new List<string>();
        ValidateRange(ChkBr.IsChecked, BrStart.Time, BrEnd.Time, "Kahvaltı", errs);
        ValidateRange(ChkS1.IsChecked, S1Start.Time, S1End.Time, "Ara Öğün 1", errs);
        ValidateRange(ChkLu.IsChecked, LuStart.Time, LuEnd.Time, "Öğle", errs);
        ValidateRange(ChkS2.IsChecked, S2Start.Time, S2End.Time, "Ara Öğün 2", errs);
        ValidateRange(ChkDi.IsChecked, DiStart.Time, DiEnd.Time, "Akşam", errs);

        if (errs.Count > 0)
        {
            Msg.TextColor = Colors.IndianRed;
            Msg.Text = string.Join("\n", errs);
            Msg.IsVisible = true;
            await ShowToast("Saat aralıklarını kontrol et.", 1700);
            return;
        }

        await ShowBusy("Program oluşturuluyor...");

        try
        {
            // 1) Başlık
            var prog = await _diet.CreateProgramHeaderAsync(
                _patient.Id,
                start,
                days,
                _patient.DailyWaterTargetMl
            );

            // 2) Seçili slotlar + saat haritası
            var selected = new List<string>();
            var times = new Dictionary<string, (TimeOnly s, TimeOnly e)>();

            void addIf(bool ok, string slot, TimeSpan s, TimeSpan e)
            {
                if (!ok) return;
                selected.Add(slot);
                times[slot] = (TimeOnly.FromTimeSpan(s), TimeOnly.FromTimeSpan(e));
            }

            addIf(ChkBr.IsChecked, "Breakfast", BrStart.Time, BrEnd.Time);
            addIf(ChkS1.IsChecked, "Snack1", S1Start.Time, S1End.Time);
            addIf(ChkLu.IsChecked, "Lunch", LuStart.Time, LuEnd.Time);
            addIf(ChkS2.IsChecked, "Snack2", S2Start.Time, S2End.Time);
            addIf(ChkDi.IsChecked, "Dinner", DiStart.Time, DiEnd.Time);

            // 3) Gün + öğün tohumla
            await _diet.SeedDaysAndMealsAsync(prog.Id, selected, times);

            await ShowToast("Program oluşturuldu.");
            await Navigation.PushAsync(new ProgramDetailPage(prog));
        }
        catch (Exception ex)
        {
            Msg.TextColor = Colors.IndianRed;
            Msg.Text = ex.Message;
            Msg.IsVisible = true;
            await ShowToast(ex.Message, 2200);
        }
        finally
        {
            await HideBusy();
        }
    }
}
