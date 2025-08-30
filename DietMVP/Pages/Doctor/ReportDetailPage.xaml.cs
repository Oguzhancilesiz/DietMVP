using DietMVP.Models;
using DietMVP.Services;

namespace DietMVP.Pages.Doctor;

public partial class ReportDetailPage : ContentPage
{
    private readonly Profile _patient;
    private readonly ReportService _report = new();

    public ReportDetailPage(Profile patient)
    {
        InitializeComponent();
        _patient = patient;
        Header.Text = $"{_patient.FullName} – Rapor Detayý";

        var today = DateTime.Today;
        FromPicker.Date = today.AddDays(-6);
        ToPicker.Date = today;
        FromPicker.MaximumDate = ToPicker.MaximumDate = today;

        DateRangeText.Text = $"{FromPicker.Date:dd.MM.yyyy} – {ToPicker.Date:dd.MM.yyyy}";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDetailAsync();
    }

    // Hýzlý aralýk çipleri
    private async void OnQuickRange(object sender, EventArgs e)
    {
        var param = (sender as Button)?.CommandParameter?.ToString() ?? "7";
        var now = DateTime.Today;

        switch (param)
        {
            case "today":
                FromPicker.Date = now;
                ToPicker.Date = now;
                break;
            case "month":
                FromPicker.Date = new DateTime(now.Year, now.Month, 1);
                ToPicker.Date = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
                break;
            case "14":
                ToPicker.Date = now;
                FromPicker.Date = now.AddDays(-13);
                break;
            default: // "7"
                ToPicker.Date = now;
                FromPicker.Date = now.AddDays(-6);
                break;
        }

        DateRangeText.Text = $"{FromPicker.Date:dd.MM.yyyy} – {ToPicker.Date:dd.MM.yyyy}";
        await LoadDetailAsync();
    }

    private async void OnApplyFilter(object sender, EventArgs e)
    {
        if (ToPicker.Date < FromPicker.Date)
        {
            await DisplayAlert("Geçersiz tarih", "Bitiþ tarihi baþlangýçtan küçük olamaz.", "Tamam");
            return;
        }
        DateRangeText.Text = $"{FromPicker.Date:dd.MM.yyyy} – {ToPicker.Date:dd.MM.yyyy}";
        await LoadDetailAsync();
    }

    private async Task LoadDetailAsync()
    {
        await WithBusy("Rapor detayý yükleniyor...", async () =>
        {
            try
            {
                var from = DateOnly.FromDateTime(FromPicker.Date);
                var to = DateOnly.FromDateTime(ToPicker.Date);

                var rep = await _report.GetReportDetailAsync(_patient.Id, from, to);

                // Özet
                SumDays.Text = rep.Summary.Days.ToString();
                SumMealPlanned.Text = rep.Summary.PlannedMeals.ToString();
                SumMealDone.Text = rep.Summary.DoneMeals.ToString();
                SumMealSkipped.Text = rep.Summary.SkippedMeals.ToString();

                var adh = rep.Summary.AdherencePct;
                SumAdh.Text = $"{adh:0}%";
                SumAdhDesc.Text = adh >= 80 ? "iyi uyum" : adh >= 50 ? "orta" : "zayýf";

                SumWaterLiters.Text = $"{rep.Summary.TotalWaterLiters:0.##}";
                AvgWaterLiters.Text = $"{rep.Summary.AvgWaterLitersPerDay:0.##}";
                SumRange.Text = rep.Summary.RangeText;

                // Günler
                DaysList.ItemsSource = rep.Days;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Veri alýnamadý: {ex.Message}", "Tamam");
                DaysList.ItemsSource = Array.Empty<ReportDetailDayVM>();
            }
        });
    }

    #region Busy helpers (ortalanmýþ kart + animasyon)
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

        // yumuþak açýlýþ animasyonu
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
    #endregion
}
